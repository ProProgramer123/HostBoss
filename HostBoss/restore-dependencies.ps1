$ErrorActionPreference = 'Stop'
$version = '0.11.6'
$expectedHash = 'D2A23832AAA948BA9A01ACC42B5726E34C5F995958F1B30D45C0E7C70B3A72D5'
$cache = Join-Path $PSScriptRoot '.packages'
$lib = Join-Path $PSScriptRoot 'lib'
New-Item -ItemType Directory -Path $cache,$lib -Force | Out-Null
$package = Join-Path $cache "mono.cecil.$version.nupkg"
if (-not (Test-Path -LiteralPath $package)) {
    Invoke-WebRequest -Uri "https://api.nuget.org/v3-flatcontainer/mono.cecil/$version/mono.cecil.$version.nupkg" -OutFile $package -UseBasicParsing
}
if ((Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash -ne $expectedHash) {
    throw 'Mono.Cecil package checksum mismatch. No DLL was extracted. Review the downloaded package before trying again.'
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($package)
try {
    $entry = $zip.GetEntry('lib/net40/Mono.Cecil.dll')
    if ($null -eq $entry) { throw 'Pinned package is missing its .NET Framework Mono.Cecil.dll.' }
    $inputStream = $entry.Open()
    try {
        $outputStream = [IO.File]::Create((Join-Path $lib 'Mono.Cecil.dll'))
        try { $inputStream.CopyTo($outputStream) } finally { $outputStream.Dispose() }
    } finally { $inputStream.Dispose() }
} finally { $zip.Dispose() }
Write-Host "Mono.Cecil $version restored and package SHA-256 verified."
