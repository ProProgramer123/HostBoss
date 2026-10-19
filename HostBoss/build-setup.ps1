param([string]$SetupSource)
$ErrorActionPreference = 'Stop'
$pin='02a97179b4db3e59be4d04850411595b170a85e3'
if (-not $SetupSource) { $SetupSource=Join-Path $PSScriptRoot '.packages\official-setup'; if (-not (Test-Path $SetupSource)) { & git clone https://github.com/swyf-modding/Setup.git $SetupSource; if($LASTEXITCODE -ne 0){throw 'Setup download failed.'}; & git -C $SetupSource checkout --detach $pin } }
if ((& git -C $SetupSource rev-parse HEAD) -ne $pin -or (& git -C $SetupSource status --porcelain --untracked-files=all)) { throw 'Use a clean official Setup checkout at the pinned commit.' }
$bep=Join-Path $PSScriptRoot '.packages\BepInEx_win_x64_5.4.23.5.zip'
if ((Get-FileHash $bep).Hash -ne '82F9878551030F54657792C0740D9D51A09500EEAE1FBA21106B0C441E6732C4') { throw 'BepInEx archive checksum mismatch.' }
& (Join-Path $PSScriptRoot 'restore-dependencies.ps1')
$stage=Join-Path $PSScriptRoot ('bin\setup-native-'+[Guid]::NewGuid().ToString('N'))
$out=Join-Path $stage 'HostBoss-Setup-1.60.4'
$payload=Join-Path $out 'SetupFiles'
$adapted=Join-Path $stage 'adapted'
New-Item -ItemType Directory -Path $payload,$adapted -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::ExtractToDirectory((Join-Path $PSScriptRoot 'dist\HostBoss-Plugin-1.60.4.zip'),(Join-Path $payload 'mod'))
Remove-Item -LiteralPath (Join-Path $payload 'mod\Repair-GameUpdate.ps1'),(Join-Path $payload 'mod\SHA256SUMS.txt')
Copy-Item -LiteralPath (Join-Path $SetupSource 'vendor') -Destination $payload -Recurse
$tempBep=Join-Path $stage 'bep'
[IO.Compression.ZipFile]::ExtractToDirectory($bep,$tempBep)
Copy-Item -LiteralPath (Join-Path $tempBep 'BepInEx') -Destination $payload -Recurse
Copy-Item -LiteralPath (Join-Path $SetupSource 'LICENSE') -Destination (Join-Path $payload 'LICENSE.Setup')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'SETUP-THIRD-PARTY.txt'),(Join-Path $PSScriptRoot 'lib\Mono.Cecil.LICENSE.txt') -Destination $payload
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'lib\Mono.Cecil.dll') -Destination $out
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'SETUP.md') -Destination (Join-Path $out 'README.md')
$names=@('GameInstall.cs','SteamEnvironment.cs','SetupException.cs','SetupLog.cs','SetupPayload.cs','LoaderInstaller.cs','CorlibSeeder.cs','SetupVerifier.cs','Patching\AwaiterUnstripper.cs','Patching\VtableBreakFixer.cs')
foreach($name in $names) {
    $text=[IO.File]::ReadAllText((Join-Path $SetupSource ('src\ScamWYF.Setup.Core\'+$name)))
    $text=$text.Replace('RuntimeInformation.IsOSPlatform(OSPlatform.Windows)','(Environment.OSVersion.Platform == PlatformID.Win32NT)')
    $text=$text.Replace('Path.GetRelativePath(source, file)','file.Substring(source.TrimEnd(Path.DirectorySeparatorChar).Length + 1)')
    $text=$text.Replace('info.ResolveLinkTarget(true)','((DirectoryInfo)null)')
    if($name -eq 'LoaderInstaller.cs') {
        $begin=$text.IndexOf('        private static void Download(')
        $end=$text.IndexOf('        private static void CopyIntoGame(', $begin)
        if($begin -lt 0 -or $end -lt 0) { throw 'Review upstream loader source.' }
        $text=$text.Substring(0,$begin) + '        private static void Download(string version,string destination,ISetupLog log) { throw new SetupException("This HostBoss installer only uses its bundled on-disk files; downloads are disabled."); }' + [Environment]::NewLine + $text.Substring($end)
        $text=$text.Replace('using System.Net.Http;','')
    }
    [IO.File]::WriteAllText((Join-Path $adapted (Split-Path $name -Leaf)),$text,[Text.UTF8Encoding]::new($false))
}
$dotnet=(Get-Command dotnet).Source
$sdk=& $dotnet --list-sdks | Select-Object -Last 1
if($sdk -notmatch '^(\S+)\s+\[(.+)\]$') { throw 'Install the .NET SDK.' }
$csc=Join-Path (Join-Path $Matches[2] $Matches[1]) 'Roslyn\bincore\csc.dll'
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$refs=@('mscorlib.dll','System.dll','System.Core.dll','System.Xml.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Windows.Forms.dll','System.Drawing.dll') | ForEach-Object { '/r:'+(Join-Path $framework $_) }
$refs+='/r:'+(Join-Path $out 'Mono.Cecil.dll')
$sources=@(Get-ChildItem $adapted -Filter *.cs | ForEach-Object FullName)
& $dotnet $csc /nologo /noconfig /nostdlib+ /target:library /deterministic+ /optimize+ "/out:$out\ScamWYF.Setup.Core.dll" $refs $sources
if($LASTEXITCODE -ne 0){throw 'Native setup core build failed.'}
$refs+='/r:'+(Join-Path $out 'ScamWYF.Setup.Core.dll')
$exe=Join-Path $out 'HostBoss-Setup-1.60.4.exe'
& $dotnet $csc /nologo /noconfig /nostdlib+ /target:winexe /platform:x64 /deterministic+ /optimize+ "/win32manifest:$PSScriptRoot\src\PluginSetup.manifest" "/out:$exe" $refs "$PSScriptRoot\src\PluginSetup.cs" "$PSScriptRoot\src\NativeInstall.cs"
if($LASTEXITCODE -ne 0){throw 'Setup EXE build failed.'}
$notice="Official Setup source: https://github.com/swyf-modding/Setup/tree/$pin`r`nAdaptations: .NET Framework path/platform APIs, network download disabled. No embedded payload, scripts, shell commands or dynamic assembly loader. Build adaptations are in HostBoss/build-setup.ps1. Mono.Cecil is 0.11.6. Licenses are visible in SetupFiles.`r`n"
[IO.File]::WriteAllText((Join-Path $payload 'SOURCES.txt'),$notice)
$lines=@(Get-ChildItem -LiteralPath $out -Recurse -File | Sort-Object FullName | ForEach-Object { (Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant()+'  '+$_.FullName.Substring($out.Length+1) })
[IO.File]::WriteAllLines((Join-Path $out 'SHA256SUMS.txt'),$lines,[Text.UTF8Encoding]::new($false))
$zip=Join-Path $PSScriptRoot 'dist\HostBoss-Setup-1.60.4.zip'
if(Test-Path -LiteralPath $zip){Remove-Item -LiteralPath $zip}
[IO.Compression.ZipFile]::CreateFromDirectory($out,$zip)
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'bin\last-setup-build.txt'),$out)
Write-Output "Built $zip`r`nEXE: $exe"
