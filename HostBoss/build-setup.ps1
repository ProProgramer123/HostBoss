param([string]$SetupSource)
$ErrorActionPreference = 'Stop'
$pin = '02a97179b4db3e59be4d04850411595b170a85e3'
if (-not $SetupSource) {
    $SetupSource = Join-Path $PSScriptRoot '.packages\official-setup'
    if (-not (Test-Path -LiteralPath $SetupSource)) {
        & git clone https://github.com/swyf-modding/Setup.git $SetupSource
        if ($LASTEXITCODE -ne 0) { throw 'Could not fetch official setup source.' }
        & git -C $SetupSource checkout --detach $pin
        if ($LASTEXITCODE -ne 0) { throw 'Could not select pinned setup source.' }
    }
}
$head = & git -C $SetupSource rev-parse HEAD
if ($LASTEXITCODE -ne 0 -or $head -ne $pin) { throw "Setup source must be official commit $pin." }
$dirty = & git -C $SetupSource status --porcelain --untracked-files=all
if ($LASTEXITCODE -ne 0 -or $dirty) { throw 'Setup source has local changes; refusing to bundle it.' }
$bep = Join-Path $PSScriptRoot '.packages\BepInEx_win_x64_5.4.23.5.zip'
if (-not (Test-Path -LiteralPath $bep)) { & (Join-Path $PSScriptRoot 'restore-plugin-dependencies.ps1') }
if ((Get-FileHash -LiteralPath $bep).Hash -ne '82F9878551030F54657792C0740D9D51A09500EEAE1FBA21106B0C441E6732C4') { throw 'BepInEx archive checksum mismatch.' }
$pluginZip = Join-Path $PSScriptRoot 'dist\HostBoss-Plugin-1.60.3.zip'
if (-not (Test-Path -LiteralPath $pluginZip)) { throw 'Build and package the 1.60.3 plugin first.' }
$stage = Join-Path $PSScriptRoot ('bin\setup-build-' + [Guid]::NewGuid().ToString('N'))
$payload = Join-Path $stage 'payload'
New-Item -ItemType Directory -Path (Join-Path $payload 'Setup'),(Join-Path $payload 'mod') -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::ExtractToDirectory($pluginZip,(Join-Path $payload 'mod'))
foreach ($file in @('setup.ps1','install-bepinex.ps1','GameDir.ps1','LICENSE','README.md','tools','vendor')) {
    Copy-Item -LiteralPath (Join-Path $SetupSource $file) -Destination (Join-Path $payload 'Setup') -Recurse -Force
}
# Upstream searches a hard-coded D: even with an explicit valid game folder.
# Resolve our already validated explicit folder first, without probing missing drives.
$resolver = Join-Path $payload 'Setup\GameDir.ps1'
$resolverText = [IO.File]::ReadAllText($resolver)
$needle = '    $candidates = @()'
if (($resolverText.Split(@($needle),[StringSplitOptions]::None).Count - 1) -ne 1) { throw 'Upstream resolver changed; review compatibility patch.' }
$explicitFirst = @'
    if ($Explicit) {
        $exact = [IO.Path]::GetFullPath($Explicit)
        if (-not (Test-Path -LiteralPath (Join-Path $exact 'Scam With Your Friends_Data\Managed\mscorlib.dll'))) { throw 'The selected game folder is incomplete.' }
        if ($RequireBepInEx -and -not (Test-Path -LiteralPath (Join-Path $exact 'BepInEx\core\BepInEx.dll'))) { throw 'BepInEx is not installed in the selected game folder.' }
        return $exact
    }
    $candidates = @()
'@
[IO.File]::WriteAllText($resolver,$resolverText.Replace($needle,$explicitFirst),[Text.UTF8Encoding]::new($false))
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'SETUP-THIRD-PARTY.txt') -Destination $payload
Copy-Item -LiteralPath $bep -Destination (Join-Path $payload 'BepInEx.zip')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Setup-HostBoss.ps1') -Destination $payload
$provenance = @"
HostBoss Setup 1.60.3
HostBoss: https://github.com/ProProgramer123/HostBoss (MIT; mod/LICENSE)
Shared mod-lib: see mod/MODLIB-SOURCE.txt and mod/LICENSE.mod-lib
Official ScamWYF Setup: https://github.com/swyf-modding/Setup/tree/$pin
Setup scripts pinned to $pin. See Setup/LICENSE.
GameDir.ps1 has one documented compatibility patch: validate and return the explicit
game folder before Steam-library probing, which otherwise fails without a D: drive.
The exact patch is in HostBoss/build-setup.ps1 in the HostBoss source repository.
Mono class libraries: see Setup/vendor/corlib/LICENSE-Mono
Doorstop: see Setup/vendor/doorstop/LICENSE
BepInEx 5.4.23.5: https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5
BepInEx archive SHA256: 82f9878551030f54657792c0740d9d51a09500eeae1fba21106b0c441e6732c4
BepInEx, HarmonyX, Harmony, MonoMod and Cecil notices: SETUP-THIRD-PARTY.txt. BepInEx source:
https://github.com/BepInEx/BepInEx/tree/v5.4.23.5
Doorstop corresponding source: https://github.com/NeighTools/UnityDoorstop/tree/v4.5.0
No game assemblies or private user data are distributed.
Inspect everything without installing: HostBoss-Setup-1.60.3.exe --extract <new-folder>
"@
[IO.File]::WriteAllText((Join-Path $payload 'SOURCES-AND-LICENSES.txt'),$provenance)
$archive = Join-Path $stage 'Payload.zip'
[IO.Compression.ZipFile]::CreateFromDirectory($payload,$archive,[IO.Compression.CompressionLevel]::Optimal,$false)
$hashFile = Join-Path $stage 'Payload.sha256'
[IO.File]::WriteAllText($hashFile,(Get-FileHash -LiteralPath $archive).Hash)
$out = Join-Path $PSScriptRoot 'dist\HostBoss-Setup-1.60.3.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll "/resource:$archive,HostBoss.Payload.zip" "/resource:$hashFile,HostBoss.Payload.sha256" "/win32manifest:$PSScriptRoot\src\PluginSetup.manifest" "/out:$out" "$PSScriptRoot\src\PluginSetup.cs"
if ($LASTEXITCODE -ne 0) { throw 'Setup EXE build failed.' }
$sum = (Get-FileHash -LiteralPath $out).Hash.ToLowerInvariant()
[IO.File]::WriteAllText(($out + '.sha256'),"$sum  HostBoss-Setup-1.60.3.exe`r`n")
Write-Output "Built $out"
