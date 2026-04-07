# =============================================================================
# TimeTrack - Script de Setup para Desenvolvimento
# =============================================================================
# Execute: .\setup-dev.ps1
# =============================================================================

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  TimeTrack - Setup de Desenvolvimento" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# -----------------------------------------------------------------------------
# 1. Verificar pre-requisitos
# -----------------------------------------------------------------------------
Write-Host "[1/6] Verificando pre-requisitos..." -ForegroundColor Yellow

# Node.js
Write-Host "  - Verificando Node.js..." -NoNewline
try {
    $nodeVersion = node --version
    Write-Host " OK ($nodeVersion)" -ForegroundColor Green
} catch {
    Write-Host " ERRO" -ForegroundColor Red
    Write-Host ""
    Write-Host "ERRO: Node.js nao encontrado!" -ForegroundColor Red
    Write-Host "Instale Node.js 20.x LTS: https://nodejs.org/" -ForegroundColor Yellow
    exit 1
}

# npm
Write-Host "  - Verificando npm..." -NoNewline
try {
    $npmVersion = npm --version
    Write-Host " OK ($npmVersion)" -ForegroundColor Green
} catch {
    Write-Host " ERRO" -ForegroundColor Red
    Write-Host ""
    Write-Host "ERRO: npm nao encontrado!" -ForegroundColor Red
    exit 1
}

# .NET SDK (opcional)
Write-Host "  - Verificando .NET SDK..." -NoNewline
try {
    $dotnetVersion = dotnet --version
    Write-Host " OK ($dotnetVersion)" -ForegroundColor Green
} catch {
    Write-Host " AVISO (opcional para UI)" -ForegroundColor Yellow
    Write-Host "    .NET SDK necessario apenas para Agent/Backend" -ForegroundColor DarkGray
}

Write-Host ""

# -----------------------------------------------------------------------------
# 2. Instalar dependencias da UI
# -----------------------------------------------------------------------------
Write-Host "[2/6] Instalando dependencias da UI..." -ForegroundColor Yellow

$uiPath = Join-Path $ProjectRoot "src\ui\timetrack-ui"

if (-not (Test-Path $uiPath)) {
    Write-Host "ERRO: Pasta da UI nao encontrada: $uiPath" -ForegroundColor Red
    exit 1
}

Set-Location $uiPath

Write-Host "  - Executando npm install..." -NoNewline
try {
    $npmOutput = npm install 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) {
        Write-Host " ERRO" -ForegroundColor Red
        Write-Host ""
        Write-Host "ERRO ao instalar dependencias:" -ForegroundColor Red
        Write-Host $npmOutput
        exit 1
    }
    Write-Host " OK" -ForegroundColor Green
} catch {
    Write-Host " ERRO" -ForegroundColor Red
    Write-Host ""
    Write-Host "ERRO: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""

# -----------------------------------------------------------------------------
# 3. Criar arquivo de ambiente
# -----------------------------------------------------------------------------
Write-Host "[3/6] Configurando ambiente..." -ForegroundColor Yellow

$envFile = Join-Path $uiPath ".env.local"
$apiUrl = "https://chronosx-timetrack-api.gpoda0.easypanel.host/api/v1"

$envContent = "VITE_API_URL=$apiUrl"

try {
    Set-Content -Path $envFile -Value $envContent -Force -Encoding UTF8
    Write-Host "  - Arquivo .env.local criado" -ForegroundColor Green
    Write-Host "  - API URL: $apiUrl" -ForegroundColor DarkGray
} catch {
    Write-Host "  - Erro ao criar .env.local: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""

# -----------------------------------------------------------------------------
# 4. Verificar build da UI
# -----------------------------------------------------------------------------
Write-Host "[4/6] Verificando build da UI..." -ForegroundColor Yellow

Write-Host "  - Executando build..." -NoNewline
try {
    # Executar build e capturar saida
    npm run build 2>&1 | Out-Null

    # Verificar se a pasta dist foi criada
    $distPath = Join-Path $uiPath "dist"
    if (Test-Path $distPath) {
        Write-Host " OK" -ForegroundColor Green
    } else {
        Write-Host " AVISO (pasta dist nao encontrada)" -ForegroundColor Yellow
    }
} catch {
    Write-Host " AVISO (nao critico para dev)" -ForegroundColor Yellow
    Write-Host "    Erro: $($_.Exception.Message)" -ForegroundColor DarkGray
}

Write-Host ""

# -----------------------------------------------------------------------------
# 5. Restaurar dependencias .NET (opcional)
# -----------------------------------------------------------------------------
Write-Host "[5/6] Restaurando dependencias .NET (opcional)..." -ForegroundColor Yellow

$solutionFile = Join-Path $ProjectRoot "TimeTrack.sln"

if (Test-Path $solutionFile) {
    Set-Location $ProjectRoot
    Write-Host "  - Executando dotnet restore..." -NoNewline
    try {
        $restoreOutput = dotnet restore $solutionFile 2>&1 | Out-String
        if ($LASTEXITCODE -eq 0) {
            Write-Host " OK" -ForegroundColor Green
        } else {
            Write-Host " AVISO (nao critico)" -ForegroundColor Yellow
        }
    } catch {
        Write-Host " AVISO (nao critico)" -ForegroundColor Yellow
    }
} else {
    Write-Host "  - Solution nao encontrada, pulando..." -ForegroundColor DarkGray
}

Write-Host ""

# -----------------------------------------------------------------------------
# 6. Pronto!
# -----------------------------------------------------------------------------
Write-Host "[6/6] Setup concluido!" -ForegroundColor Green
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  COMO INICIAR O APP" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. Abra um terminal e execute:" -ForegroundColor White
Write-Host "   cd src\ui\timetrack-ui" -ForegroundColor DarkGray
Write-Host "   npm run dev" -ForegroundColor DarkGray
Write-Host ""
Write-Host "2. Acesse no navegador:" -ForegroundColor White
Write-Host "   http://localhost:5173" -ForegroundColor DarkGray
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Perguntar se quer iniciar agora
$startNow = Read-Host "Deseja iniciar o app agora? (S/N)"
if ($startNow -eq "S" -or $startNow -eq "s") {
    Write-Host ""
    Write-Host "Iniciando a UI..." -ForegroundColor Yellow
    Set-Location $uiPath
    npm run dev
}
