$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot
$source = Join-Path $PSScriptRoot 'bin\plugin\ScamWYF.HostBoss.dll'
if (-not (Test-Path -LiteralPath $source)) { throw 'Run build-plugin.ps1 first.' }
$package = Join-Path $PSScriptRoot 'dist\HostBoss-Plugin-1.60.3'
New-Item -ItemType Directory -Path $package -Force | Out-Null
Copy-Item -LiteralPath $source -Destination (Join-Path $package 'ScamWYF.HostBoss.dll') -Force
foreach ($file in @('ScamWYF.Modding.Core.dll','LICENSE.mod-lib','MODLIB-SOURCE.txt')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot ".packages\modlib-build\$file") -Destination (Join-Path $package $file) -Force
}
Copy-Item -LiteralPath (Join-Path $repo 'LICENSE') -Destination (Join-Path $package 'LICENSE') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PLUGIN.md') -Destination (Join-Path $package 'README.md') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Repair-GameUpdate.ps1') -Destination (Join-Path $package 'Repair-GameUpdate.ps1') -Force
$hash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $package 'SHA256SUMS.txt'), "$hash  ScamWYF.HostBoss.dll`r`n")
$coreHash = (Get-FileHash (Join-Path $package 'ScamWYF.Modding.Core.dll')).Hash.ToLowerInvariant()
[IO.File]::AppendAllText((Join-Path $package 'SHA256SUMS.txt'), "$coreHash  ScamWYF.Modding.Core.dll`r`n")
$repairHash = (Get-FileHash (Join-Path $package 'Repair-GameUpdate.ps1')).Hash.ToLowerInvariant()
[IO.File]::AppendAllText((Join-Path $package 'SHA256SUMS.txt'), "$repairHash  Repair-GameUpdate.ps1`r`n")
Compress-Archive -Path (Join-Path $package '*') -DestinationPath (Join-Path $PSScriptRoot 'dist\HostBoss-Plugin-1.60.3.zip') -Force
Write-Host "Plugin package: $PSScriptRoot\dist\HostBoss-Plugin-1.60.3.zip"
