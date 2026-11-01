param([Parameter(Mandatory=$true)][string]$PackageDir)
# Rebuild the conventional offline installer using the dependencies from an
# extracted 1.60.6 setup ZIP. No game DLLs are needed for this installer build.
$ErrorActionPreference='Stop'
$package=(Resolve-Path -LiteralPath $PackageDir).Path
$out=Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Path $out -Force | Out-Null
$sdk=& dotnet --list-sdks | Select-Object -Last 1
if($sdk -notmatch '^(\S+)\s+\[(.+)\]$'){throw 'A .NET SDK is required'}
$compiler=Join-Path (Join-Path $Matches[2] $Matches[1]) 'Roslyn\bincore\csc.dll'
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$refs=@('mscorlib.dll','System.dll','System.Core.dll','System.Xml.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Windows.Forms.dll','System.Drawing.dll') | ForEach-Object {'/r:'+(Join-Path $framework $_)}
Copy-Item -LiteralPath (Join-Path $package 'Mono.Cecil.dll') -Destination $out -Force
$refs+='/r:'+(Join-Path $out 'Mono.Cecil.dll')
$sources=@(Get-ChildItem (Join-Path $PSScriptRoot 'Upstream') -Filter *.cs | ForEach-Object FullName)
& dotnet $compiler /nologo /noconfig /nostdlib+ /target:library /deterministic+ /optimize+ "/out:$out\ScamWYF.Setup.Core.dll" $refs $sources
if($LASTEXITCODE -ne 0){throw 'Setup core build failed'}
$refs+='/r:'+(Join-Path $out 'ScamWYF.Setup.Core.dll')
& dotnet $compiler /nologo /noconfig /nostdlib+ /target:winexe /platform:x64 /deterministic+ /optimize+ "/win32manifest:$PSScriptRoot\Setup.manifest" "/out:$out\HostBoss-Setup-1.60.6.exe" $refs "$PSScriptRoot\Setup.cs"
if($LASTEXITCODE -ne 0){throw 'Setup build failed'}
Copy-Item -LiteralPath (Join-Path $package 'SetupFiles') -Destination $out -Recurse -Force
$manifest=Join-Path $out 'SHA256SUMS.txt'
$lines=@(Get-ChildItem $out -Recurse -File | Where-Object FullName -NE $manifest | Sort-Object FullName | ForEach-Object {(Get-FileHash $_.FullName).Hash.ToLowerInvariant()+'  '+$_.FullName.Substring($out.Length+1)})
[IO.File]::WriteAllLines($manifest,$lines,[Text.UTF8Encoding]::new($false))
Write-Output "Built $out\HostBoss-Setup-1.60.6.exe. Keep the adjacent dependencies together."
