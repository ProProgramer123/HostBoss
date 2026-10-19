param([string]$SetupSource)
$ErrorActionPreference='Stop'
$pin='02a97179b4db3e59be4d04850411595b170a85e3'
if(-not $SetupSource){$SetupSource=Join-Path $PSScriptRoot '.packages\official-setup'}
if((& git -C $SetupSource rev-parse HEAD) -ne $pin -or (& git -C $SetupSource status --porcelain --untracked-files=all)){throw 'Use a clean official Setup checkout at the pinned commit.'}
$windows=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'bin\last-setup-build.txt')).Trim()
if(-not(Test-Path (Join-Path $windows 'SetupFiles\mod\ScamWYF.HostBoss.dll'))){throw 'Build the 1.60.4 Windows package first to stage the verified common files.'}
$stage=Join-Path $PSScriptRoot ('bin\linux-setup-'+[Guid]::NewGuid().ToString('N'))
$generated=Join-Path $stage 'generated'
$out=Join-Path $stage 'HostBoss-Linux-1.60.4-x64'
New-Item -ItemType Directory -Path $generated,$out -Force | Out-Null
$names=@('GameInstall.cs','SteamEnvironment.cs','SetupException.cs','SetupLog.cs','SetupPayload.cs','LoaderInstaller.cs','CorlibSeeder.cs','SetupVerifier.cs','Patching\AwaiterUnstripper.cs','Patching\VtableBreakFixer.cs')
foreach($name in $names){
    $text=[IO.File]::ReadAllText((Join-Path $SetupSource ('src\ScamWYF.Setup.Core\'+$name)))
    if($name -eq 'LoaderInstaller.cs'){
        $begin=$text.IndexOf('        private static void Download(');$end=$text.IndexOf('        private static void CopyIntoGame(',$begin)
        if($begin -lt 0 -or $end -lt 0){throw 'Review upstream downloader.'}
        $text=$text.Substring(0,$begin)+'        private static void Download(string version,string destination,ISetupLog log){throw new SetupException("Use the bundled offline files. Downloads are disabled.");}'+[Environment]::NewLine+$text.Substring($end)
        $text=$text.Replace('using System.Net.Http;','')
    }
    [IO.File]::WriteAllText((Join-Path $generated (Split-Path $name -Leaf)),$text,[Text.UTF8Encoding]::new($false))
}
# Adapt only filesystem boundaries; retain the verified Windows install/rollback logic.
$native=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src\NativeInstall.cs'))
$native=$native.Replace("TrimEnd('\\')",'TrimEnd(Path.DirectorySeparatorChar)')
$native=$native.Replace('root+"\\"','root+Path.DirectorySeparatorChar')
$native=$native.Replace('Path.Combine(root,relative)','Path.Combine(root,relative.Replace((char)92,Path.DirectorySeparatorChar))')
$native=$native.Replace('Path.Combine(backup,relative)','Path.Combine(backup,relative.Replace((char)92,Path.DirectorySeparatorChar))')
$native=$native.Replace('StringComparison.OrdinalIgnoreCase','(OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal)')
$native=$native.Replace('StringComparer.OrdinalIgnoreCase','(OperatingSystem.IsWindows()?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal)')
[IO.File]::WriteAllText((Join-Path $generated 'NativeInstall.cs'),$native,[Text.UTF8Encoding]::new($false))
$project=Join-Path $PSScriptRoot 'linux\HostBoss.LinuxSetup.csproj'
$cecil=Join-Path $PSScriptRoot 'lib\Mono.Cecil.dll'
$objectRoot=Join-Path $stage 'obj'
& dotnet publish $project -c Release -r linux-x64 --self-contained true -o $out --nologo "-p:HostBossGeneratedSource=$generated" "-p:HostBossCecil=$cecil" "-p:BaseIntermediateOutputPath=$objectRoot/" "-p:BaseOutputPath=$stage/build/"
if($LASTEXITCODE -ne 0){throw 'Linux publish failed.'}
Copy-Item -LiteralPath (Join-Path $windows 'SetupFiles') -Destination $out -Recurse
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LINUX.md') -Destination (Join-Path $out 'README.md')
$assets=Get-Content -LiteralPath (Join-Path $objectRoot 'project.assets.json') -Raw | ConvertFrom-Json
$runtimePackage=$null
foreach($packageRoot in $assets.packageFolders.PSObject.Properties.Name){
    $candidate=Join-Path $packageRoot 'microsoft.netcore.app.runtime.linux-x64\10.0.12'
    if(Test-Path -LiteralPath (Join-Path $candidate 'LICENSE.TXT')){$runtimePackage=$candidate;break}
}
if(-not $runtimePackage){throw 'Runtime license package was not found.'}
Copy-Item -LiteralPath (Join-Path $runtimePackage 'LICENSE.TXT') -Destination (Join-Path $out 'LICENSE.dotnet.txt')
Copy-Item -LiteralPath (Join-Path $runtimePackage 'THIRD-PARTY-NOTICES.TXT') -Destination (Join-Path $out 'THIRD-PARTY-NOTICES.dotnet.txt')
[IO.File]::WriteAllText((Join-Path $out 'SetupFiles\LINUX-SOURCES.txt'),"Linux CLI: HostBoss/linux/LinuxSetup.cs`nBuild: HostBoss/build-linux-setup.ps1`nOfficial Setup: https://github.com/swyf-modding/Setup/tree/$pin`n.NET 10 runtime: https://github.com/dotnet/runtime`nGenerated adaptations: platform path separators and disabled downloads. Shared payload matches the Windows 1.60.4 release. Linux/Proton runtime play is unverified on this Windows build machine.`n")
$lines=@(Get-ChildItem $out -Recurse -File | Where-Object Name -ne SHA256SUMS.txt | Sort-Object FullName | ForEach-Object{(Get-FileHash $_.FullName).Hash.ToLowerInvariant()+'  '+$_.FullName.Substring($out.Length+1).Replace('\','/')})
[IO.File]::WriteAllLines((Join-Path $out 'SHA256SUMS.txt'),$lines,[Text.UTF8Encoding]::new($false))
$archive=Join-Path $PSScriptRoot 'dist\HostBoss-Linux-1.60.4-x64.tar.gz'
& tar.exe -czf $archive -C $stage 'HostBoss-Linux-1.60.4-x64'
if($LASTEXITCODE -ne 0){throw 'Linux archive failed.'}
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'bin\last-linux-build.txt'),$out)
[IO.File]::WriteAllText(($archive+'.sha256'),(Get-FileHash $archive).Hash.ToLowerInvariant()+'  HostBoss-Linux-1.60.4-x64.tar.gz'+[Environment]::NewLine)
Write-Output "Linux archive: $archive`nGenerated sources: $generated"
