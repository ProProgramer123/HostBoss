param([Parameter(Mandatory=$true)][string]$GameDir)
$ErrorActionPreference='Stop'
$managed=Join-Path $GameDir 'Scam With Your Friends_Data\Managed'
$sdk=& dotnet --list-sdks | Select-Object -Last 1
if($sdk -notmatch '^(\S+)\s+\[(.+)\]$'){throw 'A .NET SDK is required.'}
$compiler=Join-Path (Join-Path $Matches[2] $Matches[1]) 'Roslyn\bincore\csc.dll'
$output=Join-Path $PSScriptRoot 'bin\plugin'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$references=@(Get-ChildItem -LiteralPath $managed -Filter *.dll | ForEach-Object {'/r:'+ $_.FullName})
$references+=@('BepInEx.dll','0Harmony.dll','ScamWYF.Modding.Core.dll') | ForEach-Object {'/r:'+(Join-Path $GameDir ('BepInEx\core\'+$_))}
$sources=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'plugin') -Filter *.cs | ForEach-Object FullName)
& dotnet $compiler /nologo /noconfig /nostdlib+ /target:library /deterministic+ /optimize+ "/out:$output\ScamWYF.HostBoss.dll" $references $sources
if($LASTEXITCODE -ne 0){throw 'HostBoss build failed.'}
Write-Output "Built $output\ScamWYF.HostBoss.dll"
