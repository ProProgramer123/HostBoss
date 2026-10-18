param([Parameter(Mandatory=$true)][string]$GameDir, [string]$TestPresentationFile)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$cache = Join-Path $root '.packages'
New-Item -ItemType Directory -Path $cache -Force | Out-Null
$package = Join-Path $cache 'newtonsoft.json.13.0.3.nupkg'
if (-not (Test-Path $package)) {
    Invoke-WebRequest -UseBasicParsing -Uri 'https://api.nuget.org/v3-flatcontainer/newtonsoft.json/13.0.3/newtonsoft.json.13.0.3.nupkg' -OutFile $package
}
if ((Get-FileHash $package).Hash -ne '872FC189E638AB1056555B03AAA38F68BCB54286E221AA646EB1129BABF63C77') { throw 'Test dependency checksum mismatch.' }
$fixture = Join-Path $root ('bin\slide-tests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($package)
$json = Join-Path $fixture 'Newtonsoft.Json.dll'
try { [IO.Compression.ZipFileExtensions]::ExtractToFile($zip.GetEntry('lib/net45/Newtonsoft.Json.dll'), $json) }
finally { $zip.Dispose() }
$managed = Join-Path $GameDir 'Scam With Your Friends_Data\Managed'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$runner = Join-Path $fixture 'SlideDocumentTests.exe'
& $compiler /nologo /target:exe "/out:$runner" "/r:$managed\Assembly-CSharp.dll" "/r:$json" `
    (Join-Path $root 'src\SlideDocument.cs') (Join-Path $PSScriptRoot 'slide-document-tests.cs')
if ($LASTEXITCODE) { throw 'Slide document test build failed.' }
foreach ($dll in @('Assembly-CSharp.dll','ScriptsAssDef.dll','UnityEngine.CoreModule.dll')) { Copy-Item (Join-Path $managed $dll) $fixture }
& $runner (Join-Path $GameDir 'Scam With Your Friends_Data\StreamingAssets\kolkata-review\review-slides.json') (Join-Path $fixture 'saves') $TestPresentationFile
if ($LASTEXITCODE) { throw 'Slide document tests failed.' }
