# Uninstall OuterWildsHeadTracking mod from OWML
$ErrorActionPreference = "Stop"

$owmlPath = Join-Path $env:APPDATA "OuterWildsModManager\OWML"
$modsPath = Join-Path $owmlPath "Mods"
$modName = "HeadCannon.OuterWildsHeadTracking"
$targetPath = Join-Path $modsPath $modName

if (-not (Test-Path $targetPath)) {
    Write-Host "Mod not installed at $targetPath"
    exit 0
}

Write-Host "Uninstalling from $targetPath"
Remove-Item -Recurse -Force $targetPath
Write-Host "Uninstall complete!"
