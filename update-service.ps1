# =============================================================================
# TimeTrack - Update Service with Latest Build
# Run as Administrator
# =============================================================================

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$BuildDir = Join-Path $ProjectRoot "build\publish\AgentService"
$ServiceDir = "C:\Program Files\ChronosX\service"
$ServiceName = "ChronosXAgent"
$DesktopHostExe = Join-Path $ProjectRoot "build\publish\DesktopHost\TimeTrack.DesktopHost.exe"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  TimeTrack - Atualizando Servico" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check admin
$currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]$currentUser
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "ERRO: Execute como Administrador!" -ForegroundColor Red
    exit 1
}

# 1. Stop service
Write-Host "[1/4] Parando servico $ServiceName..." -ForegroundColor Yellow
try {
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Write-Host "  OK" -ForegroundColor Green
} catch {
    Write-Host "  AVISO: $($_.Exception.Message)" -ForegroundColor Yellow
}

# 2. Kill any remaining processes
Write-Host "[2/4] Encerrando processos..." -ForegroundColor Yellow
Get-Process | Where-Object { $_.ProcessName -like '*TimeTrack*' } | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1
Write-Host "  OK" -ForegroundColor Green

# 3. Copy updated DLLs
Write-Host "[3/4] Copiando DLLs atualizados..." -ForegroundColor Yellow
if (-not (Test-Path $BuildDir)) {
    Write-Host "ERRO: Pasta de build nao encontrada: $BuildDir" -ForegroundColor Red
    exit 1
}
if (-not (Test-Path $ServiceDir)) {
    Write-Host "ERRO: Pasta do servico nao encontrada: $ServiceDir" -ForegroundColor Red
    exit 1
}
Copy-Item "$BuildDir\*" $ServiceDir -Force -Recurse
Write-Host "  OK" -ForegroundColor Green

# 4. Start service
Write-Host "[4/4] Iniciando servico..." -ForegroundColor Yellow
try {
    Start-Service -Name $ServiceName
    Start-Sleep -Seconds 2
    $svc = Get-Service -Name $ServiceName
    Write-Host "  Status: $($svc.Status)" -ForegroundColor Green
} catch {
    Write-Host "  ERRO: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Pronto! Reinicie o DesktopHost." -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Open DesktopHost if it exists
if (Test-Path $DesktopHostExe) {
    $start = Read-Host "Iniciar DesktopHost agora? (S/N)"
    if ($start -eq "S" -or $start -eq "s") {
        Start-Process $DesktopHostExe
    }
}
