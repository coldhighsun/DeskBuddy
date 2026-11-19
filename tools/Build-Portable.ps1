<#
.SYNOPSIS
    Publishes DeskBuddy as a single self-contained file and packs it into a portable zip.
.DESCRIPTION
    Publishes a self-contained win-x64 single-file build (the target computer needs no .NET installed), reads the version MinVer
    computes from git tags (v1.2.3), and zips the result together with the pets folder. Output goes to artifacts/portable.
    The file name has no "Setup" in it on purpose: the auto-updater only picks assets that match *Setup*.exe.
#>
param()

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\DeskBuddy.App\DeskBuddy.App.csproj"
$publishDir = Join-Path $root "artifacts\portable-publish"
$outputDir = Join-Path $root "artifacts\portable"

$version = (dotnet msbuild $project -restore -t:MinVer -getProperty:Version -nologo).Trim()
if (-not $version) {
    throw "Could not read the version from $project."
}

# Clean folders, so files removed from the project do not end up in the package.
foreach ($dir in @($publishDir, $outputDir)) {
    if (Test-Path $dir) {
        Remove-Item -Recurse -Force $dir
    }
}

# WPF's native libraries are bundled into the exe and extracted on first start, so no loose dll sits beside it.
dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $publishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed."
}

# The XML documentation files only serve the compiler.
Get-ChildItem $publishDir -Filter "*.xml" -File | Remove-Item -Force

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
$zip = Join-Path $outputDir "DeskBuddy-Portable-$version.zip"
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($publishDir, $zip, [System.IO.Compression.CompressionLevel]::Optimal, $false)

"{0} ({1:N1} MB)" -f $zip, ((Get-Item $zip).Length / 1MB)
