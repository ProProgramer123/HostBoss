param([ValidateSet('Normal','HostBoss')][string]$Mode = 'Normal')
$ErrorActionPreference = 'Stop'
$game = Split-Path $PSScriptRoot -Parent
if (Get-Process 'Scam With Your Friends' -ErrorAction SilentlyContinue) {
    throw 'Close Scam With Your Friends before switching play modes.'
}
Add-Type -Path (Join-Path $PSScriptRoot 'lib\Mono.Cecil.dll')
Add-Type -Path (Join-Path $PSScriptRoot 'lib\HostBoss.Patcher.dll')
if ($Mode -eq 'Normal') {
    [HostBoss.Install.Patcher]::Uninstall($game)
    Write-Host 'Normal mode: HostBoss patches removed. Joining uses the pre-HostBoss game files.'
} else {
    [HostBoss.Install.Patcher]::Install($game)
    Write-Host 'HostBoss mode: host a game and press F6.'
}
Start-Process -FilePath (Join-Path $game 'Scam With Your Friends.exe') -WorkingDirectory $game
