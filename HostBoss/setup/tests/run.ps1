param([Parameter(Mandatory=$true)][string]$GameDir)
$ErrorActionPreference='Stop'
$hostRoot=Split-Path (Split-Path $PSScriptRoot)
$sdk=& dotnet --list-sdks | Select-Object -Last 1
if($sdk -notmatch '^(\S+)\s+\[(.+)\]$'){throw 'SDK required'}
$sdkRoot=Join-Path $Matches[2] $Matches[1]
$compiler=Join-Path $sdkRoot 'Roslyn\bincore\csc.dll'
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$refs=@('mscorlib.dll','System.dll','System.Core.dll') | ForEach-Object {'/r:'+(Join-Path $framework $_)}
$out=Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Path $out -Force | Out-Null
foreach($mode in @('RECEIPTS','MODERN','LEGACY','UNSUPPORTED')) {
 $exe=Join-Path $out ("ReplayTests-"+$mode+'.exe')
 & dotnet $compiler /nologo /noconfig /nostdlib+ /target:exe "/define:$mode" "/out:$exe" $refs "$hostRoot\plugin\ReplayCompatibility.cs" "$PSScriptRoot\replay-compat-tests.cs"
 if($LASTEXITCODE -ne 0){throw 'Replay tests did not compile'}
 & $exe
 if($LASTEXITCODE -ne 0){throw 'Replay regression failed'}
}
$managed=Join-Path $GameDir 'Scam With Your Friends_Data\Managed'
$refs+=@(Get-ChildItem $managed -Filter *.dll | Where-Object Name -NotMatch '^(mscorlib|netstandard|System.*)\.dll$' | ForEach-Object {'/r:'+ $_.FullName})
$plugin=Join-Path $hostRoot 'bin\plugin'
$refs+='/r:'+(Join-Path $plugin 'ScamWYF.HostBoss.dll')
Copy-Item (Join-Path $sdkRoot 'TestHostNetFramework\Newtonsoft.Json.dll') $out -Force
$exe=Join-Path $out 'BossHeardTests.exe'
& dotnet $compiler /nologo /noconfig /nostdlib+ /target:exe "/out:$exe" $refs "$PSScriptRoot\boss-heard-tests.cs"
if($LASTEXITCODE -ne 0){throw 'Slide tests did not compile'}
& $exe $GameDir $plugin
if($LASTEXITCODE -ne 0){throw 'Slide regression failed'}
