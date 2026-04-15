# =============================================================================
# TimeTrack - Script de Instalacao do Desktop App
# =============================================================================
# Execute: .\install-desktop.ps1
# =============================================================================

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  TimeTrack - Instalador Desktop" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Caminho do instalador
$installerPath = Join-Path $ProjectRoot "build\installer\ChronosX-Setup-1.0.0.exe"

# Verificar se o instalador existe
if (-not (Test-Path $installerPath)) {
    Write-Host "ERRO: Instalador nao encontrado!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Caminho esperado: $installerPath" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "Para gerar o instalador, execute:" -ForegroundColor Yellow
    Write-Host "  .\installer\scripts\build-installer.ps1" -ForegroundColor DarkGray
    exit 1
}

# Informacoes do instalador
$installerInfo = Get-Item $installerPath
$installerSizeMB = [math]::Round($installerInfo.Length / 1MB, 2)

Write-Host "Instalador encontrado:" -ForegroundColor Green
Write-Host "  Arquivo: $($installerInfo.Name)" -ForegroundColor White
Write-Host "  Tamanho: $installerSizeMB MB" -ForegroundColor White
Write-Host "  Caminho: $installerPath" -ForegroundColor DarkGray
Write-Host ""

# Perguntar se quer instalar
$install = Read-Host "Deseja instalar o TimeTrack Desktop agora? (S/N)"

if ($install -eq "S" -or $install -eq "s") {
    Write-Host ""
    Write-Host "Iniciando instalador..." -ForegroundColor Yellow
    Write-Host "Siga as instrucoes na tela." -ForegroundColor DarkGray
    Write-Host ""

    # Executar instalador
    Start-Process -FilePath $installerPath -Wait

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "  Instalacao concluida!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "O TimeTrack Desktop foi instalado em:" -ForegroundColor White
    Write-Host "  C:\Program Files\ChronosX\TimeTrack" -ForegroundColor DarkGray
    Write-Host ""
    Write-Host "Para iniciar, procure por 'ChronosX' no menu Iniciar." -ForegroundColor White
    Write-Host ""
} else {
    Write-Host ""
    Write-Host "Instalador disponivel em:" -ForegroundColor Yellow
    Write-Host "  $installerPath" -ForegroundColor White
    Write-Host ""
    Write-Host "Para instalar manualmente, execute o arquivo acima." -ForegroundColor DarkGray
    Write-Host ""

    # Perguntar se quer abrir a pasta
    $openFolder = Read-Host "Deseja abrir a pasta do instalador? (S/N)"
    if ($openFolder -eq "S" -or $openFolder -eq "s") {
        explorer (Split-Path $installerPath)
    }
}
