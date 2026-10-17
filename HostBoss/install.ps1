param([string]$GameDir = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
if (Get-Process 'Scam With Your Friends' -ErrorAction SilentlyContinue) { throw 'Close the game before installing HostBoss.' }
if (-not (Test-Path (Join-Path $GameDir 'Scam With Your Friends.exe'))) { throw 'Game folder not found.' }
Add-Type -Path (Join-Path $PSScriptRoot 'lib\Mono.Cecil.dll')
Add-Type -Path (Join-Path $PSScriptRoot 'lib\HostBoss.Patcher.dll')
[HostBoss.Install.Patcher]::Install($GameDir)
Write-Host 'HostBoss installed. Host a game, then press F6.'
