param([Parameter(Mandatory=$true)][string]$GameDir, [string]$SourceDir)
$ErrorActionPreference = 'Stop'
$commit = 'efe7b332d5832f7b5efce941905df8a75795f30e'
if (-not $SourceDir) { $SourceDir = Join-Path $PSScriptRoot '.packages\mod-lib' }
if (-not (Test-Path (Join-Path $SourceDir '.git'))) {
    & git clone --depth 1 --branch v1.1.0 https://github.com/swyf-modding/mod-lib.git $SourceDir
    if ($LASTEXITCODE) { throw 'Could not restore pinned mod-lib.' }
}
$actual = & git -C $SourceDir rev-parse HEAD
if ($LASTEXITCODE -or $actual.Trim() -ne $commit) { throw 'mod-lib source does not match the pinned v1.1.0 commit.' }
& git -C $SourceDir diff --quiet HEAD -- src Version.ps1 build.ps1
if ($LASTEXITCODE) { throw 'Pinned mod-lib sources have local changes.' }
$sdk = & dotnet --list-sdks | Select-Object -Last 1
if ($sdk -notmatch '^(\S+)\s+\[(.+)\]$') { throw 'Install the .NET SDK.' }
$compiler = Join-Path (Join-Path $Matches[2] $Matches[1]) 'Roslyn\bincore\csc.dll'
$output = Join-Path $PSScriptRoot '.packages\modlib-build'
# Upstream probes D: while enumerating fallbacks even when -GameDir is supplied.
# This process-local PowerShell provider alias avoids that error without modifying upstream sources,
# mapping a Windows drive, or changing the user's Steam libraries. Explicit -GameDir is checked first.
$temporaryDrive = $false
try {
    if (-not (Get-PSDrive -Name D -ErrorAction SilentlyContinue)) { New-PSDrive -Name D -PSProvider FileSystem -Root $GameDir | Out-Null; $temporaryDrive = $true }
    & (Join-Path $SourceDir 'build.ps1') -LibraryOnly -NoCopy -GameDir $GameDir -CscDll $compiler -OutDir $output
    if ($LASTEXITCODE) { throw 'mod-lib build failed.' }
} finally { if ($temporaryDrive) { Remove-PSDrive -Name D } }
Copy-Item (Join-Path $SourceDir 'LICENSE') (Join-Path $output 'LICENSE.mod-lib') -Force
"mod-lib v1.1.0`nSource: https://github.com/swyf-modding/mod-lib/tree/$commit`nBuilt from unmodified upstream C# sources. Install ScamWYF.Modding.Core.dll in BepInEx/core only." | Set-Content (Join-Path $output 'MODLIB-SOURCE.txt')
