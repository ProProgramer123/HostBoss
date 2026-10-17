$ErrorActionPreference = 'Stop'
$game = Split-Path $PSScriptRoot
$package = Join-Path $PSScriptRoot 'dist\HostBoss-Patcher-1.4.3'
foreach ($relative in @('HostBoss\lib','HostBoss\src')) {
    New-Item -ItemType Directory -Path (Join-Path $package $relative) -Force | Out-Null
}
foreach ($name in @('HostBoss Patcher.exe','Mono.Cecil.dll','Play Normal.bat','Play Host Boss.bat')) {
    Copy-Item -LiteralPath (Join-Path $game $name) -Destination (Join-Path $package $name) -Force
}
foreach ($name in @('Mono.Cecil.dll','HostBoss.Patcher.dll','HostBoss.Runtime.dll','Mono.Cecil.LICENSE.txt')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "lib\$name") -Destination (Join-Path $package "HostBoss\lib\$name") -Force
}
foreach ($name in @('README.md','SECURITY.md','restore-dependencies.ps1','install.ps1','install.bat','uninstall.ps1','uninstall.bat','play.ps1','build.ps1','package.ps1')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $package "HostBoss\$name") -Force
}
Copy-Item -Path (Join-Path $PSScriptRoot 'src\*') -Destination (Join-Path $package 'HostBoss\src') -Force
Copy-Item -LiteralPath (Join-Path $game 'LICENSE') -Destination (Join-Path $package 'LICENSE') -Force
$hashes = Get-ChildItem -LiteralPath $package -File -Recurse | Where-Object Name -ne 'SHA256SUMS.txt' | Sort-Object FullName | ForEach-Object {
    (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_.FullName.Substring($package.Length + 1).Replace('\','/')
}
[IO.File]::WriteAllLines((Join-Path $package 'SHA256SUMS.txt'), $hashes)
$zip = Join-Path $PSScriptRoot 'dist\HostBoss-Patcher-1.4.3.zip'
Compress-Archive -Path (Join-Path $package '*') -DestinationPath $zip -Force
Write-Host "Package: $zip"
