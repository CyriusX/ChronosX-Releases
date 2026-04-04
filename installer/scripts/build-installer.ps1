# TimeTrack Installer Build Script
# Usage: .\build-installer.ps1 [-Configuration Release] [-Version "1.0.0"]

param(
    [string]$Configuration = "Release",
    [string]$Version = "1.0.0",
    [string]$InnoSetupPath = "C:\ProgramData\chocolatey\bin\ISCC.exe"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$BuildOutput = Join-Path $ProjectRoot "build"
$PublishOutput = Join-Path $BuildOutput "publish"
$InstallerOutput = Join-Path $BuildOutput "installer"
$WebView2Bootstrapper = Join-Path $ProjectRoot "installer\resources\MicrosoftEdgeWebview2Setup.exe"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "TimeTrack Installer Build Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Validate Inno Setup
if (-not (Test-Path $InnoSetupPath)) {
    Write-Error "Inno Setup not found at: $InnoSetupPath"
    Write-Host "Please install Inno Setup 6.x from https://jrsoftware.org/isinfo.php"
    exit 1
}

# Step 1: Build .NET projects (self-contained)
Write-Host "[1/5] Building AgentService..." -ForegroundColor Yellow

$AgentServiceProject = Join-Path $ProjectRoot "src\agent\TimeTrack.AgentService"
$AgentServiceOutput = Join-Path $PublishOutput "AgentService"

Push-Location $AgentServiceProject
dotnet publish -c $Configuration -r win-x64 --self-contained true -o $AgentServiceOutput /p:PublishSingleFile=false
if ($LASTEXITCODE -ne 0) {
    Pop-Location
    throw "Failed to build AgentService"
}
Pop-Location

Write-Host "[1/5] AgentService built successfully" -ForegroundColor Green

Write-Host "[2/5] Building DesktopHost..." -ForegroundColor Yellow

$DesktopHostProject = Join-Path $ProjectRoot "src\agent\TimeTrack.DesktopHost"
$DesktopHostOutput = Join-Path $PublishOutput "DesktopHost"

Push-Location $DesktopHostProject
dotnet publish -c $Configuration -r win-x64 --self-contained true -o $DesktopHostOutput /p:PublishSingleFile=false
if ($LASTEXITCODE -ne 0) {
    Pop-Location
    throw "Failed to build DesktopHost"
}
Pop-Location

Write-Host "[2/5] DesktopHost built successfully" -ForegroundColor Green

# Step 3: Build React UI
Write-Host "[3/5] Building React UI..." -ForegroundColor Yellow

$UiProject = Join-Path $ProjectRoot "src\ui\timetrack-ui"
$UiDistOutput = Join-Path $UiProject "dist"

Push-Location $UiProject
npm run build
if ($LASTEXITCODE -ne 0) {
    Pop-Location
    throw "Failed to build React UI"
}
Pop-Location

Write-Host "[3/5] React UI built successfully" -ForegroundColor Green

# Step 4: Ensure WebView2 bootstrapper exists
if (-not (Test-Path $WebView2Bootstrapper)) {
    Write-Host "[4/5] Downloading WebView2 Bootstrapper..." -ForegroundColor Yellow
    $WebView2Url = "https://go.microsoft.com/fwlink/p/?LinkId=2124703"
    try {
        Invoke-WebRequest -Uri $WebView2Url -OutFile $WebView2Bootstrapper -UseBasicParsing
        Write-Host "[4/5] WebView2 Bootstrapper downloaded" -ForegroundColor Green
    }
    catch {
        Write-Warning "Failed to download WebView2 Bootstrapper, will continue without it"
    }
}
else {
    Write-Host "[4/5] WebView2 Bootstrapper already exists" -ForegroundColor Green
}

# Step 5: Build Inno Setup Installer
Write-Host "[5/5] Building Inno Setup Installer..." -ForegroundColor Yellow

$IssFile = Join-Path $ProjectRoot "installer\timetrack-setup.iss"
$TempIssFile = Join-Path $ProjectRoot "installer\timetrack-setup-temp.iss"

# Read and update version in .iss file
$IssContent = Get-Content $IssFile -Raw
$IssContent = $IssContent -replace '#define MyAppVersion ".*"', "#define MyAppVersion ""$Version"""
$IssContent = $IssContent -replace '#define MyAppVersionStr ".*"', "#define MyAppVersionStr ""$Version.0"""

Set-Content -Path $TempIssFile -Value $IssContent -Encoding UTF8

# Ensure output directory exists
if (-not (Test-Path $InstallerOutput)) {
    New-Item -ItemType Directory -Path $InstallerOutput -Force | Out-Null
}

# Build installer
& $InnoSetupPath $TempIssFile /DOutputBase=ChronosX-Setup-$Configuration-$Version /DOutputDir=$InstallerOutput

if ($LASTEXITCODE -ne 0) {
    Remove-Item $TempIssFile -Force
    throw "Failed to build installer"
}

# Clean up temp file
Remove-Item $TempIssFile -Force

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "Installer built successfully!" -ForegroundColor Green
Write-Host "Output: $InstallerOutput\ChronosX-Setup-$Configuration-$Version.exe" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
