$ErrorActionPreference = 'Stop'
$cache = Join-Path $PSScriptRoot '.packages'
$version = '5.4.23.5'
$package = Join-Path $cache "BepInEx_win_x64_$version.zip"
$expectedHash = '82F9878551030F54657792C0740D9D51A09500EEAE1FBA21106B0C441E6732C4'
New-Item -ItemType Directory -Path $cache -Force | Out-Null
if (-not (Test-Path -LiteralPath $package)) {
    Invoke-WebRequest -Uri "https://github.com/BepInEx/BepInEx/releases/download/v$version/BepInEx_win_x64_$version.zip" -OutFile $package -UseBasicParsing
}
if ((Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash -ne $expectedHash) { throw 'Pinned BepInEx package checksum mismatch.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$destination = Join-Path $cache 'bepinex'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$zip = [IO.Compression.ZipFile]::OpenRead($package)
try {
    foreach ($name in @('BepInEx.dll','0Harmony.dll')) {
        $entry = $zip.GetEntry("BepInEx/core/$name")
        if ($null -eq $entry) { throw "Pinned BepInEx package lacks $name." }
        $inputStream = $entry.Open()
        try {
            $outputStream = [IO.File]::Create((Join-Path $destination $name))
            try { $inputStream.CopyTo($outputStream) } finally { $outputStream.Dispose() }
        } finally { $inputStream.Dispose() }
    }
} finally { $zip.Dispose() }
Write-Host 'BepInEx/Harmony build references restored with verified package SHA-256. Nothing installed into the game.'
