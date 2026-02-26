# Deploy OuterWildsHeadTracking mod to OWML Mods folder
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$buildOutput = Join-Path $projectRoot "src\OuterWildsHeadTracking\bin\Release\net48"
$manifest = Get-Content (Join-Path $projectRoot "manifest.json") | ConvertFrom-Json
$modName = $manifest.uniqueName

# Import shared game detection module
$modulePath = Join-Path $projectRoot "headcannon-core\powershell\GamePathDetection.psm1"
Import-Module $modulePath -Force

# Outer Wilds uses OWML mod manager
$owmlPath = Find-OWMLPath
if (-not $owmlPath) {
    Write-Host "ERROR: OWML not found!" -ForegroundColor Red
    Write-Host "Outer Wilds Mod Manager must be installed." -ForegroundColor Yellow
    Write-Host "Expected path: $env:APPDATA\OuterWildsModManager\OWML" -ForegroundColor Gray
    exit 1
}

$modsPath = Join-Path $owmlPath "Mods"
$targetPath = Join-Path $modsPath $modName

Write-Host "Deploying to $targetPath"

# Create mod folder if needed
if (-not (Test-Path $targetPath)) {
    New-Item -ItemType Directory -Path $targetPath | Out-Null
}

# Copy built DLLs (including shared library)
$dllsToCopy = @(
    "OuterWildsHeadTracking.dll",
    "HeadCannon.Core.dll"
)

foreach ($dll in $dllsToCopy) {
    $source = Join-Path $buildOutput $dll
    if (Test-Path $source) {
        Copy-Item $source $targetPath -Force
        Write-Host "  Copied $dll"
    } else {
        Write-Warning "  DLL not found: $source"
    }
}

# Copy manifest.json
Copy-Item (Join-Path $projectRoot "manifest.json") $targetPath -Force
Write-Host "  Copied manifest.json"

# Copy default-config.json
Copy-Item (Join-Path $projectRoot "default-config.json") $targetPath -Force
Write-Host "  Copied default-config.json"

Write-Host "Deploy complete!"
