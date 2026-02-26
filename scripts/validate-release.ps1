#!/usr/bin/env pwsh
#Requires -Version 5.1
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir

Write-Host "=== Outer Wilds Head Tracking - Release Validation ===" -ForegroundColor Cyan
Write-Host ""

$allPassed = $true

foreach ($file in @("README.md", "LICENSE")) {
    Write-Host "Checking $file..." -ForegroundColor Gray
    if (Test-Path (Join-Path $projectRoot $file)) {
        Write-Host "  $file exists" -ForegroundColor Green
    } else {
        Write-Host "  ERROR: $file not found" -ForegroundColor Red
        $allPassed = $false
    }
}

Write-Host "Checking manifest.json..." -ForegroundColor Gray
if (Test-Path (Join-Path $projectRoot "manifest.json")) {
    Write-Host "  manifest.json exists" -ForegroundColor Green
} else {
    Write-Host "  ERROR: manifest.json not found" -ForegroundColor Red
    $allPassed = $false
}

Write-Host "Checking build output..." -ForegroundColor Gray
$dllPath = Join-Path $projectRoot "src\OuterWildsHeadTracking\bin\Release\net48\OuterWildsHeadTracking.dll"
if (Test-Path $dllPath) {
    $dllInfo = Get-Item $dllPath
    Write-Host "  OuterWildsHeadTracking.dll exists ($($dllInfo.Length) bytes)" -ForegroundColor Green
} else {
    Write-Host "  WARNING: OuterWildsHeadTracking.dll not found" -ForegroundColor Yellow
    $allPassed = $false
}

Write-Host ""
Write-Host "===============================" -ForegroundColor Cyan

if ($allPassed) {
    Write-Host "All validation checks passed!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "Some validation checks failed." -ForegroundColor Yellow
    exit 1
}
