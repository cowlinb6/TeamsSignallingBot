<#
.SYNOPSIS
    Builds the Teams app package (appPackage.zip) from this folder.

.DESCRIPTION
    The three files MUST sit at the ZIP root - no parent folder. Uses
    System.IO.Compression so entries are added flat (Compress-Archive would
    preserve relative paths).
#>
$ErrorActionPreference = "Stop"
Set-Location -Path $PSScriptRoot

$files = @("manifest.json", "color.png", "outline.png")
foreach ($f in $files) {
    if (-not (Test-Path -LiteralPath $f -PathType Leaf)) {
        Write-Error "Missing $f (run .\generate-icons.py for placeholder icons)"
        exit 1
    }
}

$zipPath = Join-Path $PSScriptRoot "appPackage.zip"
Remove-Item -LiteralPath $zipPath -ErrorAction SilentlyContinue

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$zip = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($f in $files) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $zip,
            (Join-Path $PSScriptRoot $f),
            $f,
            [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally {
    $zip.Dispose()
}

Write-Host "Built $zipPath"
[System.IO.Compression.ZipFile]::OpenRead($zipPath).Entries |
    Select-Object @{ Name = "Length"; Expression = { $_.Length } }, Name |
    Format-Table -AutoSize
