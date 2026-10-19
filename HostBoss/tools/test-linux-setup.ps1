param([Parameter(Mandatory=$true)][string]$GameDir)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$package=[IO.File]::ReadAllText((Join-Path $root 'bin\last-linux-build.txt')).Trim()
$stage=Split-Path $package
$generated=Join-Path $stage 'generated'
$fixture=Join-Path $root ('bin\linux-host-tests-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
$hostConfig=Join-Path $fixture 'host-runtime.json'
[IO.File]::WriteAllText($hostConfig,'{"runtimeOptions":{"tfm":"net10.0","framework":{"name":"Microsoft.NETCore.App","version":"10.0.12"}}}')
foreach($option in @('--self-check','--help','--version','--launch-option')){
    & dotnet exec --runtimeconfig $hostConfig (Join-Path $package 'hostboss-setup.dll') $option
    if($LASTEXITCODE -ne 0){throw "Portable CLI check failed: $option"}
}
$elf=[IO.File]::ReadAllBytes((Join-Path $package 'hostboss-setup'))
if($elf[0] -ne 0x7f -or $elf[1] -ne 0x45 -or $elf[2] -ne 0x4c -or $elf[3] -ne 0x46 -or $elf[4] -ne 2 -or $elf[18] -ne 0x3e){throw 'Linux x64 ELF header not found.'}
foreach($name in @('libhostfxr.so','libhostpolicy.so','libcoreclr.so','LICENSE.dotnet.txt','THIRD-PARTY-NOTICES.dotnet.txt')){if(-not(Test-Path (Join-Path $package $name))){throw "Missing runtime file: $name"}}
Write-Output 'PASS: Linux ELF x64 target, runtime files, licenses and package checksums.'
$testSources=Join-Path $fixture 'generated';Copy-Item -LiteralPath $generated -Destination $testSources -Recurse
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'native-setup-tests.cs') -Destination (Join-Path $testSources 'SetupTest.cs')
$runner=Join-Path $fixture 'runner'
Copy-Item -LiteralPath $package -Destination $runner -Recurse
& dotnet build (Join-Path $root 'linux\HostBoss.LinuxSetup.csproj') -c Release -r win-x64 --self-contained false -o $runner --nologo '-p:StartupObject=SetupTest' "-p:HostBossGeneratedSource=$testSources" "-p:HostBossCecil=$root/lib/Mono.Cecil.dll" "-p:BaseIntermediateOutputPath=$fixture/obj/" "-p:BaseOutputPath=$fixture/build/"
if($LASTEXITCODE -ne 0){throw 'Host logic test build failed.'}
$game=Join-Path $fixture 'game'
New-Item -ItemType Directory -Path (Join-Path $game 'Scam With Your Friends_Data') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $GameDir 'Scam With Your Friends.exe') -Destination $game
Copy-Item -LiteralPath (Join-Path $GameDir 'Scam With Your Friends_Data\Managed') -Destination (Join-Path $game 'Scam With Your Friends_Data\Managed') -Recurse
& dotnet (Join-Path $runner 'hostboss-setup.dll') $game (Join-Path $GameDir 'Scam With Your Friends_Data\Managed')
if($LASTEXITCODE -ne 0){throw 'Portable installer logic tests failed.'}
Write-Output 'PASS: portable installer logic tested on Windows host. Linux runtime and Proton gameplay remain untested.'
