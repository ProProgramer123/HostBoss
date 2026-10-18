param([Parameter(Mandatory=$true)][string]$GameDir)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$exe = Join-Path $root 'dist\HostBoss-Setup-1.60.3.exe'
$fixture = Join-Path $root ('bin\setup-tests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
$payload = Join-Path $fixture 'payload'
$p = Start-Process -FilePath $exe -ArgumentList @('--self-check') -WindowStyle Hidden -Wait -PassThru
if ($p.ExitCode -ne 0) { throw 'Embedded payload failed verification.' }
$uiError = Join-Path $fixture 'ui-error.txt'
$p = Start-Process -FilePath $exe -ArgumentList @('--ui-self-check') -RedirectStandardError $uiError -WindowStyle Hidden -Wait -PassThru
if ($p.ExitCode -ne 0) { throw ('Setup window failed initialization: ' + (Get-Content -LiteralPath $uiError -Raw)) }
Write-Output 'PASS: graphical setup form and controls initialize.'
$p = Start-Process -FilePath $exe -ArgumentList @('--extract',('"' + $payload + '"')) -WindowStyle Hidden -Wait -PassThru
if ($p.ExitCode -ne 0) { throw 'Payload extraction failed.' }
foreach ($file in @('Setup-HostBoss.ps1','BepInEx.zip','mod\ScamWYF.HostBoss.dll','mod\ScamWYF.Modding.Core.dll','Setup\vendor\doorstop\.doorstop_version','Setup\vendor\corlib\LICENSE-Mono','SOURCES-AND-LICENSES.txt')) {
    if (-not (Test-Path -LiteralPath (Join-Path $payload $file))) { throw "Missing embedded file: $file" }
}
Write-Output 'PASS: self-check, offline payload extraction, required files and licenses.'
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (-not (Test-Path -LiteralPath (Join-Path $payload 'SETUP-THIRD-PARTY.txt'))) { throw 'Missing dependency notices.' }
$game = Join-Path $fixture 'game'
New-Item -ItemType Directory -Path $game -Force | Out-Null
# Fixture copies are private compiler/setup inputs; never packaged or published.
Copy-Item -LiteralPath (Join-Path $GameDir 'Scam With Your Friends.exe') -Destination $game
Copy-Item -LiteralPath (Join-Path $GameDir 'Scam With Your Friends_Data\Managed') -Destination (Join-Path $game 'Scam With Your Friends_Data\Managed') -Recurse
$managed = Join-Path $game 'Scam With Your Friends_Data\Managed'
$before = @(Get-ChildItem -LiteralPath $managed -Filter *.dll | Get-FileHash | ForEach-Object { $_.Hash + ' ' + [IO.Path]::GetFileName($_.Path) })
$script = Join-Path $payload 'Setup-HostBoss.ps1'
# Scope the process mock to this isolated test invocation, never the shipped script.
function global:Get-Process { param($Name,$ErrorAction) @() }
try {
    & $script -GameDir $game
    foreach ($file in @('BepInEx\plugins\ScamWYF.HostBoss.dll','BepInEx\core\ScamWYF.Modding.Core.dll')) {
        $name = Split-Path $file -Leaf
        if ((Get-FileHash (Join-Path $game $file)).Hash -ne (Get-FileHash (Join-Path $payload ('mod\' + $name))).Hash) { throw "Install mismatch: $name" }
    }
    Write-Output 'PASS: full clean install sets up the loader and both mod DLLs offline.'
    $after = @(Get-ChildItem -LiteralPath $managed -Filter *.dll | Get-FileHash | ForEach-Object { $_.Hash + ' ' + [IO.Path]::GetFileName($_.Path) })
    if (Compare-Object $before $after) { throw 'Setup changed game Managed assemblies.' }
    Write-Output 'PASS: game Managed assemblies remain byte-for-byte unchanged.'
    $saves = Join-Path $game 'HostBoss\Presentations'; New-Item -ItemType Directory -Path $saves -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $saves 'keep.json'),'user fixture')
    & $script -GameDir $game -ModsOnly
    if ([IO.File]::ReadAllText((Join-Path $saves 'keep.json')) -ne 'user fixture') { throw 'Update touched saved data.' }
    if (-not (Get-ChildItem -LiteralPath (Join-Path $game 'HostBoss\backup') -Recurse -Filter ScamWYF.HostBoss.dll)) { throw 'Existing plugin was not backed up.' }
    Write-Output 'PASS: repeated mod install backs up existing DLLs and keeps saves.'
    # Lock the destination plugin to force an update failure after the core copy.
    $core = Join-Path $game 'BepInEx\core\ScamWYF.Modding.Core.dll'
    $plugin = Join-Path $game 'BepInEx\plugins\ScamWYF.HostBoss.dll'
    Add-Type -Path (Join-Path $game 'BepInEx\core\Mono.Cecil.dll')
    $assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($core)
    $assembly.Name.Version = [Version]'1.0.0.0'
    $assembly.Write(($core + '.fixture')); $assembly.Dispose()
    Copy-Item -LiteralPath ($core + '.fixture') -Destination $core -Force
    $coreHash = (Get-FileHash $core).Hash
    $lock = [IO.File]::Open($plugin,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
    $failed = $false
    try { & $script -GameDir $game -ModsOnly } catch { $failed = $true } finally { $lock.Dispose() }
    if (-not $failed -or (Get-FileHash $core).Hash -ne $coreHash) { throw 'Failed update did not preserve the core.' }
    Write-Output 'PASS: failed update rolls back already written files.'
    $assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($core)
    $assembly.Name.Version = [Version]'9.0.0.0'
    $assembly.Write(($core + '.fixture')); $assembly.Dispose()
    Copy-Item -LiteralPath ($core + '.fixture') -Destination $core -Force
    $newerHash = (Get-FileHash $core).Hash
    & $script -GameDir $game -ModsOnly
    if ((Get-FileHash $core).Hash -ne $newerHash) { throw 'Newer shared library was overwritten.' }
    Write-Output 'PASS: newer shared library is preserved.'
    $legacy = Join-Path $managed 'Assembly-CSharp.dll'
    [IO.File]::WriteAllText($legacy,'fixture HostBoss.Runtime marker')
    $failed=$false; try { & $script -GameDir $game -ModsOnly } catch { $failed=$_.Exception.Message -match 'Legacy HostBoss' }
    if (-not $failed) { throw 'Legacy patch was not rejected.' }
    Write-Output 'PASS: legacy disk patches are rejected before installation.'
    Remove-Item Function:\global:Get-Process
    function global:Get-Process { param($Name,$ErrorAction) [pscustomobject]@{Id=999999;ProcessName='Scam With Your Friends'} }
    $failed=$false; try { & $script -GameDir $game } catch { $failed=$_.Exception.Message -match 'Close the game' }
    if (-not $failed) { throw 'Running game was not rejected.' }
    Write-Output 'PASS: running-game guard refuses installation.'
} finally { Remove-Item Function:\global:Get-Process -ErrorAction SilentlyContinue }
Write-Output "Setup integration checks passed. Isolated fixture retained at $fixture"
