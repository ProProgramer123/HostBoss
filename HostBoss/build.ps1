param([string]$GameDir = (Split-Path $PSScriptRoot))
$ErrorActionPreference = 'Stop'
$managed = Join-Path $GameDir 'Scam With Your Friends_Data\Managed'
foreach ($name in @('Assembly-CSharp.dll','ScriptsAssDef.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $managed $name))) { throw "Missing game reference $name. Pass -GameDir with your installed game's folder." }
}
if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'lib\Mono.Cecil.dll'))) { throw 'Run HostBoss\restore-dependencies.ps1 before building.' }
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
$dotnetExe = if ($null -ne $dotnet) { $dotnet.Source } else { Join-Path $env:ProgramFiles 'dotnet\dotnet.exe' }
$sdkLine = & $dotnetExe --list-sdks | Select-Object -Last 1
if ($LASTEXITCODE -ne 0 -or $sdkLine -notmatch '^(\S+)\s+\[(.+)\]$') { throw 'Install the .NET SDK (8 or newer) to build HostBoss.' }
$compiler = Join-Path (Join-Path $Matches[2] $Matches[1]) 'Roslyn\bincore\csc.dll'
$refs = Get-ChildItem $managed -Filter *.dll | Where-Object {$_.Name -ne 'HostBoss.Runtime.dll'} | ForEach-Object {'/r:' + $_.FullName}
& $dotnetExe $compiler /nologo /noconfig /target:library /nostdlib+ "/out:$PSScriptRoot\lib\HostBoss.Runtime.dll" $refs "$PSScriptRoot\src\HostBoss.cs"
if ($LASTEXITCODE -ne 0) { throw 'Runtime build failed.' }
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:library "/r:$PSScriptRoot\lib\Mono.Cecil.dll" "/out:$PSScriptRoot\lib\HostBoss.Patcher.dll" "$PSScriptRoot\src\Patcher.cs"
if ($LASTEXITCODE -ne 0) { throw 'Patcher build failed.' }
$desktop = Join-Path (Split-Path $PSScriptRoot) 'HostBoss Patcher.exe'
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /platform:anycpu /r:System.Windows.Forms.dll /r:System.Drawing.dll "/r:$PSScriptRoot\lib\Mono.Cecil.dll" "/win32manifest:$PSScriptRoot\src\Installer.manifest" "/out:$desktop" "$PSScriptRoot\src\Installer.cs" "$PSScriptRoot\src\Patcher.cs"
if ($LASTEXITCODE -ne 0) { throw 'Desktop patcher build failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'lib\Mono.Cecil.dll') -Destination (Join-Path (Split-Path $PSScriptRoot) 'Mono.Cecil.dll') -Force
