<#
.SYNOPSIS
    Publishes DeskBuddy and packs it into a setup program with Inno Setup 7.
.DESCRIPTION
    Publishes a self-contained win-x64 build (so the target computer needs no .NET installed), reads the version from
    DeskBuddy.App.csproj, and runs ISCC on installer/DeskBuddy.iss. Output goes to artifacts/installer.
.PARAMETER IsccPath
    Path to ISCC.exe of Inno Setup 7. Defaults to the per-user winget install location.
#>
param(
    [string] $IsccPath = (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 7\ISCC.exe")
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\DeskBuddy.App\DeskBuddy.App.csproj"
$publishDir = Join-Path $root "artifacts\publish"
$outputDir = Join-Path $root "artifacts\installer"

if (-not (Test-Path $IsccPath)) {
    throw "ISCC.exe not found at '$IsccPath'. Install Inno Setup 7 or pass -IsccPath."
}

$version = (dotnet msbuild $project -getProperty:Version -nologo).Trim()
if (-not $version) {
    throw "Could not read the version from $project."
}

# A clean folder, so files removed from the project do not end up in the installer.
if (Test-Path $publishDir) {
    Remove-Item -Recurse -Force $publishDir
}

dotnet publish $project -c Release -r win-x64 --self-contained true -p:DebugType=none -o $publishDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed."
}

& $IsccPath "/DAppVersion=$version" "/DPublishDir=$publishDir" "/DOutputDir=$outputDir" (Join-Path $root "installer\DeskBuddy.iss")
if ($LASTEXITCODE -ne 0) {
    throw "ISCC failed."
}

$setup = Join-Path $outputDir "DeskBuddy-Setup-$version.exe"
"{0} ({1:N1} MB)" -f $setup, ((Get-Item $setup).Length / 1MB)
