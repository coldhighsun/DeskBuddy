<#
.SYNOPSIS
    Publishes DeskBuddy as portable zips: one with the .NET runtime and one without it.
.DESCRIPTION
    Builds two win-x64 packages, reads the version MinVer computes from git tags (v1.2.3), and zips each one together with the
    pets folder. Output goes to artifacts/portable:
      DeskBuddy-Portable-<version>-win-x64-with-runtime.zip  self-contained, a plain folder (exe plus its dll files); runs without .NET installed.
      DeskBuddy-Portable-<version>-win-x64-no-runtime.zip    framework-dependent single exe; needs the .NET 11 Desktop Runtime (x64) installed.
    The file names have no "Setup" in them on purpose: the auto-updater only picks assets that match *Setup*.exe.
.PARAMETER Variant
    Which package to build: WithRuntime, NoRuntime or All (the default).
#>
param(
    [ValidateSet("WithRuntime", "NoRuntime", "All")]
    [string]$Variant = "All"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\DeskBuddy.App\DeskBuddy.App.csproj"
$publishRoot = Join-Path $root "artifacts\portable-publish"
$outputDir = Join-Path $root "artifacts\portable"

$version = (dotnet msbuild $project -restore -t:MinVer -getProperty:Version -nologo).Trim()
if (-not $version) {
    throw "Could not read the version from $project."
}

# Clean folders, so files removed from the project do not end up in the package.
foreach ($dir in @($publishRoot, $outputDir)) {
    if (Test-Path $dir) {
        Remove-Item -Recurse -Force $dir
    }
}
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

Add-Type -AssemblyName System.IO.Compression.FileSystem

function New-PortableZip {
    param(
        [string]$Name,
        [string[]]$PublishArgs
    )

    $publishDir = Join-Path $publishRoot $Name
    dotnet publish $project -c Release -r win-x64 @PublishArgs -o $publishDir
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed ($Name)."
    }

    # The XML documentation files only serve the compiler.
    Get-ChildItem $publishDir -Filter "*.xml" -File | Remove-Item -Force

    $zip = Join-Path $outputDir "DeskBuddy-Portable-$version-win-x64-$Name.zip"
    [System.IO.Compression.ZipFile]::CreateFromDirectory($publishDir, $zip, [System.IO.Compression.CompressionLevel]::Optimal, $false)
    "{0} ({1:N1} MB)" -f $zip, ((Get-Item $zip).Length / 1MB)
}

if ($Variant -in @("WithRuntime", "All")) {
    # A plain folder: WPF's native libraries would have to be unpacked to a temp folder on every first start in a single file.
    New-PortableZip -Name "with-runtime" -PublishArgs @("--self-contained", "true")
}

if ($Variant -in @("NoRuntime", "All")) {
    # The native WPF libraries live in the installed runtime, so a single file needs no self-extraction.
    New-PortableZip -Name "no-runtime" -PublishArgs @("--self-contained", "false", "-p:PublishSingleFile=true")
}
