#!/usr/bin/env pwsh
#Requires -Version 5.1
# Package OuterWildsHeadTracking for release
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$buildOutput = Join-Path $projectRoot "src\OuterWildsHeadTracking\bin\Release\net48"
$releaseDir = Join-Path $projectRoot "release"

Write-Host "=== Outer Wilds Head Tracking - Package Release ===" -ForegroundColor Magenta
Write-Host ""

# Create release folder
if (-not (Test-Path $releaseDir)) {
    New-Item -ItemType Directory -Path $releaseDir | Out-Null
}

# Get version from manifest
$manifest = Get-Content (Join-Path $projectRoot "manifest.json") | ConvertFrom-Json
$version = $manifest.version
Write-Host "Version: $version" -ForegroundColor Cyan
Write-Host ""

$packageName = "OuterWildsHeadTracking-$version"
$stagingDir = Join-Path $releaseDir $packageName
$zipPath = Join-Path $releaseDir "$packageName.zip"

# Clean previous package
if (Test-Path $stagingDir) { Remove-Item -Recurse -Force $stagingDir }
if (Test-Path $zipPath) { Remove-Item -Force $zipPath }

# Create package folder
New-Item -ItemType Directory -Path $stagingDir | Out-Null

Write-Host "Copying release files..." -ForegroundColor Cyan

# Copy DLLs
$dlls = @("OuterWildsHeadTracking.dll", "CameraUnlock.Core.dll")
foreach ($dll in $dlls) {
    $source = Join-Path $buildOutput $dll
    if (-not (Test-Path $source)) {
        Write-Host "ERROR: $dll not found. Run 'pixi run build' first." -ForegroundColor Red
        exit 1
    }
    Copy-Item $source $stagingDir
    Write-Host "  $dll" -ForegroundColor Green
}

# Copy OWML manifest and config (consumed by the Outer Wilds Mod Manager)
Copy-Item (Join-Path $projectRoot "manifest.json") $stagingDir
Write-Host "  manifest.json" -ForegroundColor Green
Copy-Item (Join-Path $projectRoot "default-config.json") $stagingDir
Write-Host "  default-config.json" -ForegroundColor Green

# Stamp launcher-manifest.json with the real release version and copy it to
# the ZIP root. This is additive metadata for the Lopari launcher; the OWML
# files above are untouched, so the Outer Wilds Mod Manager flow is unchanged.
$manifestSource = Join-Path $projectRoot "launcher-manifest.json"
if (-not (Test-Path $manifestSource)) {
    Write-Host "ERROR: launcher-manifest.json not found at repo root ($manifestSource)" -ForegroundColor Red
    exit 1
}
$launcherManifest = Get-Content $manifestSource -Raw | ConvertFrom-Json
$launcherManifest.mod_info.version = $version
# Set-Content -Encoding UTF8 on Windows PowerShell 5.1 writes a BOM that
# serde_json rejects; write through the .NET API with a no-BOM encoder.
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText(
    (Join-Path $stagingDir "launcher-manifest.json"),
    ($launcherManifest | ConvertTo-Json -Depth 10),
    $utf8NoBom
)
Write-Host "  launcher-manifest.json (v$version)" -ForegroundColor Green

# Copy documentation
$docFiles = @("README.md", "LICENSE")
foreach ($doc in $docFiles) {
    $docPath = Join-Path $projectRoot $doc
    if (Test-Path $docPath) {
        Copy-Item $docPath -Destination $stagingDir -Force
        Write-Host "  $doc" -ForegroundColor Green
    } else {
        Write-Host "  WARNING: $doc not found" -ForegroundColor Yellow
    }
}

Write-Host ""

# Create zip
Write-Host "Creating ZIP archive..." -ForegroundColor Cyan
Compress-Archive -Path "$stagingDir\*" -DestinationPath $zipPath

# Cleanup temp folder
Remove-Item -Recurse -Force $stagingDir

$zipSize = (Get-Item $zipPath).Length / 1KB
Write-Host ""
Write-Host "=== Package Complete ===" -ForegroundColor Magenta
Write-Host ""
Write-Host "Release archive created:" -ForegroundColor Green
Write-Host "  $zipPath" -ForegroundColor Cyan
Write-Host ("  Size: {0:N1} KB" -f $zipSize) -ForegroundColor White
