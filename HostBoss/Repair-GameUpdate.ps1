<#
Run with the game closed after updating from an older ScamWYF setup.
Only an older Unity.InputSystem compatibility override is moved to backup.
The installed game's Managed files, mods and saved presentations are untouched.
#>
[CmdletBinding(SupportsShouldProcess=$true)]
param([Parameter(Mandatory=$true)][string]$GameDir)
$ErrorActionPreference = 'Stop'
$gameRoot = (Resolve-Path -LiteralPath $GameDir).Path.TrimEnd('\','/')
$managedInput = Join-Path $gameRoot 'Scam With Your Friends_Data\Managed\Unity.InputSystem.dll'
$oldInput = Join-Path $gameRoot 'unstripped_corlib\Unity.InputSystem.dll'
if (-not (Test-Path -LiteralPath $managedInput)) { throw 'Choose the installed Scam With Your Friends game folder.' }
if (Get-Process -Name 'Scam With Your Friends' -ErrorAction SilentlyContinue) { throw 'Close Scam With Your Friends before repairing its compatibility override.' }
if (-not (Test-Path -LiteralPath $oldInput)) { Write-Output 'OK: no Input System override; the game uses its current version.'; return }
$gameVersion = [Reflection.AssemblyName]::GetAssemblyName($managedInput).Version
$overrideVersion = [Reflection.AssemblyName]::GetAssemblyName($oldInput).Version
if ($overrideVersion -ge $gameVersion) { Write-Output "No change: override $overrideVersion is not older than game $gameVersion."; return }
$backup = [IO.Path]::GetFullPath((Join-Path $gameRoot ('HostBoss\backup\Unity.InputSystem-before-game-update-' + [Guid]::NewGuid().ToString('N') + '.dll')))
if (-not $backup.StartsWith($gameRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Backup path escaped the game folder.' }
if ($PSCmdlet.ShouldProcess($oldInput, "Back up outdated Input System $overrideVersion so the game can load $gameVersion")) {
    New-Item -ItemType Directory -Path (Split-Path -Parent $backup) -Force | Out-Null
    Move-Item -LiteralPath $oldInput -Destination $backup
    Write-Output "REPAIRED: backed up Input System $overrideVersion. The game will now load its own $gameVersion assembly."
    Write-Output "Backup: $backup"
}
