# Revert Outer Wilds to vanilla (unmodded) state
# For OWML-based mods, vanilla is the same as uninstall since we don't install OWML
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host ""
Write-Host "=== Outer Wilds - Revert to Vanilla ===" -ForegroundColor Magenta
Write-Host ""

# Just run uninstall - OWML is managed separately by the Outer Wilds Mod Manager
& (Join-Path $scriptDir "uninstall.ps1")
