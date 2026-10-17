$ErrorActionPreference = 'Stop'
$fixture = Join-Path $PSScriptRoot 'test-game'
# Validation only checks for the executable's presence. Never launch the fixture.
$fixtureExe = Join-Path $fixture 'Scam With Your Friends.exe'
if (-not (Test-Path -LiteralPath $fixtureExe)) { [IO.File]::WriteAllBytes($fixtureExe, (New-Object byte[] 0)) }
$standalone = Join-Path $PSScriptRoot 'standalone-test'
$exe = Join-Path $standalone 'HostBoss Patcher.exe'
$package = Join-Path (Split-Path $PSScriptRoot) 'dist\HostBoss-Patcher-1.4.3'
New-Item -ItemType Directory -Path $standalone -Force | Out-Null
Copy-Item -Path (Join-Path $package '*') -Destination $standalone -Recurse -Force
function Run-Patcher([string[]]$PatcherArguments, [int]$Expected = 0) {
    $out = Join-Path $standalone 'stdout.txt'
    $err = Join-Path $standalone 'stderr.txt'
    $process = Start-Process -FilePath $exe -ArgumentList $PatcherArguments -WorkingDirectory $standalone -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput $out -RedirectStandardError $err
    if ($process.ExitCode -ne $Expected) { throw "Patcher exited $($process.ExitCode), expected $Expected. $(Get-Content $err -Raw)" }
    return (Get-Content $out -Raw)
}
$before = @{}
foreach ($name in @('Assembly-CSharp.dll','ScriptsAssDef.dll')) { $before[$name] = (Get-FileHash (Join-Path $fixture "Scam With Your Friends_Data\Managed\$name")).Hash }
$self = Run-Patcher @('--self-check')
if ($self -notmatch 'PASS') { throw 'Package file check failed.' }
# An incomplete distribution must fail before patching either game assembly.
$runtimePath = Join-Path $standalone 'HostBoss\lib\HostBoss.Runtime.dll'
Move-Item -LiteralPath $runtimePath -Destination ($runtimePath + '.missing')
try {
    Run-Patcher @('--install', ('"' + $fixture + '"')) 1 | Out-Null
    foreach ($name in @('Assembly-CSharp.dll','ScriptsAssDef.dll')) {
        if ((Get-FileHash (Join-Path $fixture "Scam With Your Friends_Data\Managed\$name")).Hash -ne $before[$name]) { throw "Incomplete package changed $name." }
    }
} finally { Move-Item -LiteralPath ($runtimePath + '.missing') -Destination $runtimePath }
Run-Patcher @('--install', ('"' + $fixture + '"')) | Out-Null
$state = Run-Patcher @('--status', ('"' + $fixture + '"'))
if ($state -notmatch 'enabled') { throw "Unexpected state after install: $state" }
Run-Patcher @('--install', ('"' + $fixture + '"')) | Out-Null
# A missing backup stamp must prevent removal, leaving patched game files intact.
$stamp = Join-Path $fixture 'HostBoss\backup\Assembly-CSharp.dll.sha256'
$stampText = [IO.File]::ReadAllText($stamp)
[IO.File]::Delete($stamp)
Run-Patcher @('--remove', ('"' + $fixture + '"')) 1 | Out-Null
[IO.File]::WriteAllText($stamp,$stampText)
Run-Patcher @('--remove', ('"' + $fixture + '"')) | Out-Null
foreach ($name in @('Assembly-CSharp.dll','ScriptsAssDef.dll')) {
    if ((Get-FileHash (Join-Path $fixture "Scam With Your Friends_Data\Managed\$name")).Hash -ne $before[$name]) { throw "EXE removal did not restore $name exactly." }
}
$state = Run-Patcher @('--status', ('"' + $fixture + '"'))
if ($state -notmatch 'disabled') { throw "Unexpected state after removal: $state" }
Run-Patcher @('--install', ('"' + $standalone + '"')) 1 | Out-Null
$report = @('PASS: conventional EXE/DLL package loads without embedded assemblies.', 'PASS: incomplete package fails before changing game assemblies.', 'PASS: EXE installs and updates the copied game.', 'PASS: missing backup metadata prevents removal.', 'PASS: EXE removes the mod and restores exact original bytes.', 'PASS: invalid game folders are rejected.')
$report | Add-Content (Join-Path (Split-Path $PSScriptRoot) 'verification.txt')
$report
