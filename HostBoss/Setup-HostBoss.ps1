[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$GameDir, [switch]$ModsOnly)
$ErrorActionPreference = 'Stop'
$gameRoot = (Resolve-Path -LiteralPath $GameDir).Path.TrimEnd('\','/')
foreach ($name in @('Scam With Your Friends.exe','Scam With Your Friends_Data\Managed\Assembly-CSharp.dll','Scam With Your Friends_Data\Managed\ScriptsAssDef.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $gameRoot $name) -PathType Leaf)) { throw 'Select the game folder containing Scam With Your Friends.exe.' }
}
if (Get-Process -Name 'Scam With Your Friends' -ErrorAction SilentlyContinue) { throw 'Close the game before installing HostBoss.' }
foreach ($name in @('Assembly-CSharp.dll','ScriptsAssDef.dll')) {
    $bytes = [IO.File]::ReadAllBytes((Join-Path $gameRoot ('Scam With Your Friends_Data\Managed\' + $name)))
    if ([Text.Encoding]::ASCII.GetString($bytes).Contains('HostBoss.Runtime')) { throw 'Legacy HostBoss disk patch detected. Use the old patcher: Remove / Normal Play, then run this setup again.' }
}
$backupRoot = Join-Path $gameRoot ('HostBoss\backup\setup-' + [Guid]::NewGuid().ToString('N'))
$changes = New-Object 'System.Collections.Generic.List[object]'
function Backup-Target([string]$Relative) {
    $path = [IO.Path]::GetFullPath((Join-Path $gameRoot $Relative))
    if (-not $path.StartsWith($gameRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Backup target escaped game folder.' }
    $exists = Test-Path -LiteralPath $path -PathType Leaf
    $backup = Join-Path $backupRoot $Relative
    if ($exists) {
        New-Item -ItemType Directory -Path (Split-Path $backup) -Force | Out-Null
        Copy-Item -LiteralPath $path -Destination $backup
    }
    $changes.Add([pscustomobject]@{Path=$path; Backup=$backup; Existed=$exists})
}
function Install-Dll([string]$Name, [string]$Folder) {
    $source = Join-Path $PSScriptRoot ('mod\' + $Name)
    $target = Join-Path $gameRoot ('BepInEx\' + $Folder + '\' + $Name)
    if ($Folder -eq 'core' -and (Test-Path -LiteralPath $target)) {
        $existing = [Reflection.AssemblyName]::GetAssemblyName($target).Version
        $bundled = [Reflection.AssemblyName]::GetAssemblyName($source).Version
        if ($existing -gt $bundled) { Write-Output "Keeping newer shared library $existing."; return }
    }
    New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
    $pending = $target + '.hostboss-' + [Guid]::NewGuid().ToString('N') + '.new'
    try {
        Copy-Item -LiteralPath $source -Destination $pending
        if (Test-Path -LiteralPath $target) { [IO.File]::Replace($pending,$target,($pending + '.old')) }
        else { [IO.File]::Move($pending,$target) }
    } finally {
        if (Test-Path -LiteralPath $pending) { Remove-Item -LiteralPath $pending }
        if (Test-Path -LiteralPath ($pending + '.old')) { Remove-Item -LiteralPath ($pending + '.old') }
    }
    if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $target).Hash) { throw "Installed checksum mismatch: $Name" }
    Write-Output "Installed: $target"
}
try {
    # Snapshot all paths the official setup can write, plus the two mod DLLs.
    $targets = @('BepInEx\plugins\ScamWYF.HostBoss.dll','BepInEx\core\ScamWYF.Modding.Core.dll')
    if (-not $ModsOnly) {
        $targets += @('winhttp.dll','.doorstop_version','doorstop_config.ini')
        $targets += @(Get-ChildItem -LiteralPath (Join-Path $gameRoot 'Scam With Your Friends_Data\Managed') -Filter *.dll | ForEach-Object { 'unstripped_corlib\' + $_.Name })
        $targets += @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Setup\vendor\corlib') -Filter *.dll | ForEach-Object { 'unstripped_corlib\' + $_.Name })
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $zip = [IO.Compression.ZipFile]::OpenRead((Join-Path $PSScriptRoot 'BepInEx.zip'))
        try { $targets += @($zip.Entries | Where-Object { $_.FullName.StartsWith('BepInEx/core/') -and $_.Name } | ForEach-Object { $_.FullName.Replace('/','\') }) } finally { $zip.Dispose() }
    }
    foreach ($target in ($targets | Select-Object -Unique)) { Backup-Target $target }
    if (-not $ModsOnly) {
        # Run the pinned upstream setup in its own process: it calls exit.
        $setup = Join-Path $PSScriptRoot 'Setup\setup.ps1'
        $pack = Join-Path $PSScriptRoot 'BepInEx.zip'
        & "$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File $setup -GameDir $gameRoot -BepInExZip $pack
        if ($LASTEXITCODE -ne 0) { throw "Official loader setup failed (exit $LASTEXITCODE)." }
        & (Join-Path $PSScriptRoot 'mod\Repair-GameUpdate.ps1') -GameDir $gameRoot
        if ((Get-Content -LiteralPath (Join-Path $gameRoot 'doorstop_config.ini') -Raw) -notmatch 'dll_search_path_override\s*=\s*unstripped_corlib') { throw 'Existing Doorstop configuration needs repair using the launcher Setup tab.' }
    }
    foreach ($name in @('BepInEx\core\BepInEx.dll','BepInEx\core\0Harmony.dll','winhttp.dll','unstripped_corlib\mscorlib.dll')) {
        if (-not (Test-Path -LiteralPath (Join-Path $gameRoot $name))) { throw 'Mod loader is incomplete. Run the full setup instead of --mods-only.' }
    }
    Install-Dll 'ScamWYF.Modding.Core.dll' 'core'
    Install-Dll 'ScamWYF.HostBoss.dll' 'plugins'
    Write-Output "Backups: $backupRoot"
    Write-Output 'HostBoss installed successfully. Start the game, host a lobby and press F6.'
} catch {
    $failure = $_
    Write-Output 'Installation failed; restoring files changed by this setup.'
    foreach ($change in $changes) {
        try {
            if ($change.Existed) {
                if ((Test-Path -LiteralPath $change.Path -PathType Leaf) -and (Get-FileHash -LiteralPath $change.Backup).Hash -eq (Get-FileHash -LiteralPath $change.Path).Hash) { continue }
                Copy-Item -LiteralPath $change.Backup -Destination $change.Path -Force
            }
            elseif (Test-Path -LiteralPath $change.Path -PathType Leaf) { Remove-Item -LiteralPath $change.Path }
        } catch { Write-Warning "Could not restore $($change.Path): $($_.Exception.Message). Backup: $($change.Backup)" }
    }
    throw $failure
}
