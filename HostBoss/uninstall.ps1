param([string]$GameDir = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
if (Get-Process 'Scam With Your Friends' -ErrorAction SilentlyContinue) { throw 'Close the game before uninstalling HostBoss.' }
Add-Type -Path (Join-Path $PSScriptRoot 'lib\Mono.Cecil.dll')
Add-Type -Path (Join-Path $PSScriptRoot 'lib\HostBoss.Patcher.dll')
[HostBoss.Install.Patcher]::Uninstall($GameDir)
Write-Host 'HostBoss removed; the files backed up at installation were restored.'
