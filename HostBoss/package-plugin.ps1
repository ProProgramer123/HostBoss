$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot
$source = Join-Path $PSScriptRoot 'bin\plugin\ScamWYF.HostBoss.dll'
if (-not (Test-Path -LiteralPath $source)) { throw 'Run build-plugin.ps1 first.' }
$package = Join-Path $PSScriptRoot 'dist\HostBoss-Plugin-1.5.1'
New-Item -ItemType Directory -Path $package -Force | Out-Null
Copy-Item -LiteralPath $source -Destination (Join-Path $package 'ScamWYF.HostBoss.dll') -Force
Copy-Item -LiteralPath (Join-Path $repo 'LICENSE') -Destination (Join-Path $package 'LICENSE') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PLUGIN.md') -Destination (Join-Path $package 'README.md') -Force
$hash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $package 'SHA256SUMS.txt'), "$hash  ScamWYF.HostBoss.dll`r`n")
Compress-Archive -Path (Join-Path $package '*') -DestinationPath (Join-Path $PSScriptRoot 'dist\HostBoss-Plugin-1.5.1.zip') -Force
Write-Host "Plugin package: $PSScriptRoot\dist\HostBoss-Plugin-1.5.1.zip"
