param([Parameter(Mandatory=$true)][string]$GameDir)
$ErrorActionPreference = 'Stop'
$managed = Join-Path $GameDir 'Scam With Your Friends_Data\Managed'
foreach ($name in @('Assembly-CSharp.dll','ScriptsAssDef.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $managed $name))) { throw "Missing game build reference: $name" }
}
$dependencyRoot = Join-Path $PSScriptRoot '.packages\bepinex'
$modLib = Join-Path $PSScriptRoot '.packages\modlib-build\ScamWYF.Modding.Core.dll'
if (-not (Test-Path $modLib)) { throw 'Run restore-modlib.ps1 -GameDir <game folder> first.' }
foreach ($name in @('BepInEx.dll','0Harmony.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $dependencyRoot $name))) { throw 'Run restore-plugin-dependencies.ps1 first.' }
}
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
$dotnetExe = if ($null -ne $dotnet) { $dotnet.Source } else { Join-Path $env:ProgramFiles 'dotnet\dotnet.exe' }
$sdkLine = & $dotnetExe --list-sdks | Select-Object -Last 1
if ($LASTEXITCODE -ne 0 -or $sdkLine -notmatch '^(\S+)\s+\[(.+)\]$') { throw 'Install the .NET SDK (8 or newer).' }
$compiler = Join-Path (Join-Path $Matches[2] $Matches[1]) 'Roslyn\bincore\csc.dll'
$output = Join-Path $PSScriptRoot 'bin\plugin'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$refs = @(Get-ChildItem -LiteralPath $managed -Filter *.dll | Where-Object Name -ne 'HostBoss.Runtime.dll' | ForEach-Object { '/r:' + $_.FullName })
$refs += @(('/r:' + (Join-Path $dependencyRoot 'BepInEx.dll')), ('/r:' + (Join-Path $dependencyRoot '0Harmony.dll')))
$refs += '/r:' + $modLib
& $dotnetExe $compiler /nologo /noconfig /target:library /nostdlib+ /define:BEPINEX /deterministic+ /optimize+ "/out:$output\ScamWYF.HostBoss.dll" $refs "$PSScriptRoot\src\HostBoss.cs" "$PSScriptRoot\src\Plugin.cs" "$PSScriptRoot\src\SlideDocument.cs" "$PSScriptRoot\src\SlideImages.cs" "$PSScriptRoot\src\SlideMaker.cs" "$PSScriptRoot\src\LaptopApps.cs"
if ($LASTEXITCODE -ne 0) { throw 'HostBoss BepInEx plugin build failed.' }
Write-Host "Built $output\ScamWYF.HostBoss.dll. No game files changed."
