# TimeTrack Update Config Generator
# Generates the "Updates" section for appsettings.json with correct checksum and file size
#
# Usage:
#   .\generate-update-config.ps1 -Version "1.1.0"
#   .\generate-update-config.ps1 -Version "1.1.0" -InstallerPath ".\build\installer\ChronosX-Setup-Release-1.1.0.exe"
#   .\generate-update-config.ps1 -Version "1.1.0" -DownloadUrl "https://cdn.cyrius.com/chronosx/ChronosX-Setup-1.1.0.exe"

param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$InstallerPath,

    [string]$DownloadUrl = "https://cdn.cyrius.com/chronosx/ChronosX-Setup-{VERSION}.exe",

    [string]$Channel = "stable",

    [string]$ReleaseNotes = "",

    [switch]$IsMandatory,

    [string]$MinimumVersion = "",

    [string[]]$Channels = @("stable", "beta"),

    [switch]$CopyToClipboard,

    [switch]$UpdateAppSettings
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "TimeTrack Update Config Generator" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Resolve installer path
if ([string]::IsNullOrEmpty($InstallerPath)) {
    $InstallerPath = Join-Path $ProjectRoot "build\installer\ChronosX-Setup-Release-$Version.exe"
}

# Check if installer exists
if (-not (Test-Path $InstallerPath)) {
    # Try alternative paths
    $altPaths = @(
        (Join-Path $ProjectRoot "build\installer\ChronosX-Setup-$Version.exe"),
        (Join-Path $ProjectRoot "build\installer\ChronosX-Setup-Debug-$Version.exe"),
        (Join-Path $ProjectRoot "build\installer\TimeTrack-Setup-Release-$Version.exe"),
        (Join-Path $ProjectRoot "build\installer\TimeTrack-Setup-$Version.exe")
    )

    foreach ($altPath in $altPaths) {
        if (Test-Path $altPath) {
            $InstallerPath = $altPath
            break
        }
    }

    if (-not (Test-Path $InstallerPath)) {
        Write-Error "Installer not found at: $InstallerPath`nTried alternative paths:`n  - $($altPaths -join "`n  - ")`n`nPlease specify -InstallerPath or build the installer first."
        exit 1
    }
}

Write-Host "Installer: $InstallerPath" -ForegroundColor Gray
$InstallerPath = (Resolve-Path $InstallerPath).Path

# Calculate SHA256 checksum
Write-Host "[1/3] Calculating SHA256 checksum..." -ForegroundColor Yellow
$hashResult = Get-FileHash -Path $InstallerPath -Algorithm SHA256
$ChecksumSha256 = $hashResult.Hash.ToLower()
Write-Host "  Checksum: $ChecksumSha256" -ForegroundColor Green

# Get file size
Write-Host "[2/3] Getting file size..." -ForegroundColor Yellow
$fileInfo = Get-Item $InstallerPath
$FileSizeBytes = $fileInfo.Length
$FileSizeMB = [math]::Round($FileSizeBytes / 1MB, 2)
Write-Host "  Size: $FileSizeBytes bytes ($FileSizeMB MB)" -ForegroundColor Green

# Format download URL
$DownloadUrl = $DownloadUrl -replace '\{VERSION\}', $Version

# Format release notes
if ([string]::IsNullOrEmpty($ReleaseNotes)) {
    $ReleaseNotes = "## ChronosX $Version`n`n- Release notes to be added"
}

# Escape release notes for JSON
$ReleaseNotesEscaped = $ReleaseNotes -replace '\\', '\\' -replace '"', '\"' -replace "`n", '\n' -replace "`r", ''

# Generate JSON config
Write-Host "[3/3] Generating configuration..." -ForegroundColor Yellow

$channelsJson = ($Channels | ForEach-Object { "`"$_`"" }) -join ", "

$jsonConfig = @"
  "Updates": {
    "LatestVersion": "$Version",
    "DownloadUrl": "$DownloadUrl",
    "ChecksumSha256": "$ChecksumSha256",
    "FileSizeBytes": $FileSizeBytes,
    "ReleaseNotes": "$ReleaseNotesEscaped",
    "MinimumVersion": "$MinimumVersion",
    "IsMandatory": $($IsMandatory.ToString().ToLower()),
    "Channels": [$channelsJson]
  }
"@

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "Generated Configuration" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host $jsonConfig -ForegroundColor White
Write-Host ""

# Copy to clipboard if requested
if ($CopyToClipboard) {
    Set-Clipboard -Value $jsonConfig
    Write-Host "Configuration copied to clipboard!" -ForegroundColor Cyan
}

# Update appsettings.json if requested
if ($UpdateAppSettings) {
    $appSettingsPath = Join-Path $ProjectRoot "src\backend\TimeTrack.Api\appsettings.json"

    if (-not (Test-Path $appSettingsPath)) {
        Write-Error "appsettings.json not found at: $appSettingsPath"
        exit 1
    }

    $content = Get-Content $appSettingsPath -Raw

    # Use regex to replace the Updates section
    $pattern = '(?s)("Updates"\s*:\s*\{[^}]*\})'

    if ($content -match $pattern) {
        $newContent = $content -replace $pattern, $jsonConfig.Trim()
        Set-Content -Path $appSettingsPath -Value $newContent -Encoding UTF8
        Write-Host "Updated: $appSettingsPath" -ForegroundColor Green
    }
    else {
        Write-Warning "Could not find 'Updates' section in appsettings.json"
        Write-Host "Please add the following manually:" -ForegroundColor Yellow
        Write-Host $jsonConfig
    }
}

# Output summary
Write-Host ""
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "  Version:        $Version" -ForegroundColor Gray
Write-Host "  Download URL:   $DownloadUrl" -ForegroundColor Gray
Write-Host "  Checksum:       $ChecksumSha256" -ForegroundColor Gray
Write-Host "  File Size:      $FileSizeBytes bytes ($FileSizeMB MB)" -ForegroundColor Gray
Write-Host "  Is Mandatory:   $IsMandatory" -ForegroundColor Gray
Write-Host ""

# Usage examples
Write-Host "Usage:" -ForegroundColor Cyan
Write-Host "  -CopyToClipboard       Copy config to clipboard" -ForegroundColor Gray
Write-Host "  -UpdateAppSettings     Update appsettings.json automatically" -ForegroundColor Gray
Write-Host "  -IsMandatory           Mark as mandatory update" -ForegroundColor Gray
Write-Host "  -ReleaseNotes '...'    Custom release notes" -ForegroundColor Gray
