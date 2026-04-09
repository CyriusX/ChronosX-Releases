# =============================================================================
# TimeTrack - Update Agent with Latest Build
# Run as Administrator
# =============================================================================

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$BuildDir = Join-Path $ProjectRoot "build\publish\AgentService"
$ServiceDir = "C:\Program Files\ChronosX\service"
$TaskName = "ChronosX Agent"
$DesktopHostExe = Join-Path $ProjectRoot "build\publish\DesktopHost\TimeTrack.DesktopHost.exe"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  TimeTrack - Atualizando Agente" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check admin
$currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]$currentUser
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "ERRO: Execute como Administrador!" -ForegroundColor Red
    exit 1
}

# 1. Stop scheduled task + kill processes
Write-Host "[1/4] Parando agente..." -ForegroundColor Yellow
try {
    Stop-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
} catch { }
Get-Process | Where-Object { $_.ProcessName -like '*TimeTrack*' } | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1
Write-Host "  OK" -ForegroundColor Green

# 2. Copy updated files
Write-Host "[2/4] Copiando arquivos atualizados..." -ForegroundColor Yellow
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

# 3. Ensure task still exists (re-register if needed)
Write-Host "[3/4] Verificando tarefa agendada..." -ForegroundColor Yellow
$task = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
if (-not $task) {
    Write-Host "  Tarefa nao encontrada, re-registrando..." -ForegroundColor Yellow
    $agentExe = Join-Path $ServiceDir "TimeTrack.AgentService.exe"
    $action = New-ScheduledTaskAction -Execute $agentExe -WorkingDirectory $ServiceDir
    $trigger = New-ScheduledTaskTrigger -AtLogOn
    $trigger.Delay = 'PT10S'
    $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit 0 -MultipleInstances IgnoreNew -StartWhenAvailable
    $principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Highest
    Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Force | Out-Null
    Write-Host "  Tarefa registrada." -ForegroundColor Green
} else {
    Write-Host "  OK (tarefa existe)" -ForegroundColor Green
}

# 4. Start task
Write-Host "[4/4] Iniciando agente..." -ForegroundColor Yellow
try {
    Start-ScheduledTask -TaskName $TaskName
    Start-Sleep -Seconds 2
    $state = (Get-ScheduledTask -TaskName $TaskName).State
    Write-Host "  Estado: $state" -ForegroundColor Green
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
        Start-Process $DesktopHostExe -WorkingDirectory (Split-Path $DesktopHostExe)
    }
}
