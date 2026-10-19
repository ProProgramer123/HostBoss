param([Parameter(Mandatory=$true)][string]$GameDir)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$package=[IO.File]::ReadAllText((Join-Path $root 'bin\last-setup-build.txt')).Trim()
$fixture=Join-Path $root ('bin\native-setup-tests-'+[Guid]::NewGuid().ToString('N'))
$copy=Join-Path $fixture 'installer'
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
Copy-Item -LiteralPath $package -Destination $copy -Recurse
$exe=Join-Path $copy 'HostBoss-Setup-1.60.4.exe'
foreach($mode in @('--self-check','--ui-self-check')) {
    $errorFile=Join-Path $fixture ($mode.TrimStart('-')+'.txt')
    $p=Start-Process -FilePath $exe -ArgumentList $mode -RedirectStandardError $errorFile -WindowStyle Hidden -Wait -PassThru
    if($p.ExitCode -ne 0){throw (Get-Content $errorFile -Raw)}
}
$asm=[Reflection.Assembly]::ReflectionOnlyLoadFrom($exe)
if($asm.GetManifestResourceNames().Count -ne 0){throw 'Unexpected embedded resources.'}
Write-Output 'PASS: external payload validation, graphical initialization and zero embedded resources.'
$game=Join-Path $fixture 'game'
New-Item -ItemType Directory -Path (Join-Path $game 'Scam With Your Friends_Data') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $GameDir 'Scam With Your Friends.exe') -Destination $game
Copy-Item -LiteralPath (Join-Path $GameDir 'Scam With Your Friends_Data\Managed') -Destination (Join-Path $game 'Scam With Your Friends_Data\Managed') -Recurse
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
& (Join-Path $framework 'csc.exe') /nologo /target:exe /main:SetupTest "/out:$copy\SetupTest.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll "/r:$copy\ScamWYF.Setup.Core.dll" "/r:$copy\Mono.Cecil.dll" "$root\src\PluginSetup.cs" "$root\src\NativeInstall.cs" "$PSScriptRoot\native-setup-tests.cs"
if($LASTEXITCODE -ne 0){throw 'Test harness build failed.'}
& (Join-Path $copy 'SetupTest.exe') $game (Join-Path $GameDir 'Scam With Your Friends_Data\Managed')
if($LASTEXITCODE -ne 0){throw 'Native install tests failed.'}
$sample=Join-Path $copy 'SetupFiles\mod\ScamWYF.HostBoss.dll'; [IO.File]::AppendAllText($sample,'test corruption')
$p=Start-Process -FilePath $exe -ArgumentList '--self-check' -RedirectStandardError (Join-Path $fixture 'corruption.txt') -WindowStyle Hidden -Wait -PassThru
if($p.ExitCode -eq 0){throw 'Corrupt payload accepted.'}
Write-Output 'PASS: modified payload refused.'
Write-Output "Native setup integration checks passed. Fixture: $fixture"
