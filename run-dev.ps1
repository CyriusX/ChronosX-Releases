# =============================================================================
# TimeTrack - Script para Rodar o App (Modo Desenvolvimento)
# =============================================================================
# Execute: .\run-dev.ps1
# =============================================================================

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$uiPath = Join-Path $ProjectRoot "src\ui\timetrack-ui"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  TimeTrack - Iniciando App" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Verificar se dependências estão instaladas
if (-not (Test-Path (Join-Path $uiPath "node_modules"))) {
    Write-Host "Dependências não encontradas. Execute primeiro: .\setup-dev.ps1" -ForegroundColor Red
    exit 1
}

# Verificar se .env.local existe
$envFile = Join-Path $uiPath ".env.local"
if (-not (Test-Path $envFile)) {
    Write-Host "Arquivo .env.local não encontrado. Execute primeiro: .\setup-dev.ps1" -ForegroundColor Red
    exit 1
}

Write-Host "Iniciando UI em modo desenvolvimento..." -ForegroundColor Yellow
Write-Host "URL: http://localhost:5173" -ForegroundColor DarkGray
Write-Host "Pressione Ctrl+C para parar" -ForegroundColor DarkGray
Write-Host ""

Set-Location $uiPath
npm run dev
