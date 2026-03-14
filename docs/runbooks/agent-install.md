# Runbook: Instalação do Agent TimeTrack

## Visão Geral

O TimeTrack Agent é um Windows Service que roda em background para rastrear tempo de atividades do usuário.

| Componente | Descrição |
|------------|-----------|
| **AgentService** | Windows Service de tracking |
| **DesktopHost** | Aplicação UI com WebView2 |
| **SQLite DB** | Banco local (~10MB típico) |

---

## Pré-requisitos

| Requisito | Versão Mínima |
|-----------|---------------|
| Windows | 10 (Build 17763+) |
| .NET Runtime | 8.0.x |
| WebView2 Runtime | Latest |

### Verificar Pré-requisitos

```powershell
# Verificar versão do Windows
winver

# Verificar .NET 8
dotnet --list-runtimes | findstr "Microsoft.WindowsDesktop.App 8"

# Verificar WebView2
reg query "HKLM\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" /v pv
```

---

## Instalação

### Opção 1: MSIX (Recomendado)

#### Instalação Manual

1. Baixar o pacote `TimeTrack.Agent_x.x.x_x64.msix`
2. Clicar duas vezes no arquivo
3. Clicar em "Instalar"
4. Aguardar conclusão

#### Instalação Silenciosa (Enterprise)

```powershell
# Instalar silenciosamente
Add-AppxPackage -Path "TimeTrack.Agent_x.x.x_x64.msix"

# Verificar instalação
Get-AppxPackage -Name "CyriusX.TimeTrack*"
```

### Opção 2: Manual (Desenvolvimento)

```powershell
# Clonar repositório
git clone https://github.com/cyriusx/timetrack.git
cd timetrack

# Restaurar dependências
dotnet restore TimeTrack.sln

# Build
dotnet build src/agent/TimeTrack.AgentService -c Release

# Registrar como serviço (requer admin)
sc.exe create "TimeTrack Agent" binPath="C:\Caminho\Para\TimeTrack.AgentService.exe" start=auto
```

---

## Configuração

### Arquivo de Configuração

Local: `%LOCALAPPDATA%\CyriusX\TimeTrack\appsettings.json`

```json
{
  "Agent": {
    "PollingIntervalMs": 1000,
    "IdleThresholdSeconds": 60,
    "DatabasePath": "timetrack.db",
    "EnableDiagnostics": false,
    "Sync": {
      "BackendUrl": "https://api.timetrack.com",
      "SyncIntervalSeconds": 60,
      "MaxBatchSize": 100
    }
  }
}
```

### Parâmetros de Configuração

| Parâmetro | Default | Descrição |
|-----------|---------|-----------|
| `PollingIntervalMs` | 1000 | Intervalo de verificação de janela ativa |
| `IdleThresholdSeconds` | 60 | Segundos para considerar idle |
| `DatabasePath` | timetrack.db | Caminho do SQLite (relativo ou absoluto) |
| `EnableDiagnostics` | false | Habilita logs detalhados |
| `Sync:BackendUrl` | - | URL do backend para sincronização |
| `Sync:SyncIntervalSeconds` | 60 | Intervalo entre sincronizações |
| `Sync:MaxBatchSize` | 100 | Máximo de mensagens por batch |

### Variáveis de Ambiente

| Variável | Descrição |
|----------|-----------|
| `TIMETRACK_PROCESS_PRIORITY` | Prioridade do processo (BelowNormal, Normal, Low) |

---

## Verificação Pós-Instalação

### 1. Verificar Serviço Rodando

```powershell
# Status do serviço
Get-Service -Name "TimeTrack Agent"

# Expected: Status = Running
```

### 2. Verificar Logs de Inicialização

```powershell
# Visualizar logs recentes
Get-Content "$env:LOCALAPPDATA\CyriusX\TimeTrack\logs\agent.log" -Tail 20

# Ou via Event Viewer
Get-WinEvent -LogName Application -FilterXPath "*[System[Provider[@Name='TimeTrack']]]" -MaxEvents 10
```

### 3. Verificar Conectividade com Backend

```powershell
# Testar endpoint de health
Invoke-WebRequest -Uri "https://api.timetrack.com/health" -UseBasicParsing
```

### 4. Verificar Banco Local

```powershell
# Verificar se DB foi criado
Test-Path "$env:LOCALAPPDATA\CyriusX\TimeTrack\timetrack.db"

# Verificar tamanho
(Get-Item "$env:LOCALAPPDATA\CyriusX\TimeTrack\timetrack.db").Length / 1MB
```

---

## Operações Comuns

### Iniciar Serviço

```powershell
Start-Service -Name "TimeTrack Agent"
```

### Parar Serviço

```powershell
Stop-Service -Name "TimeTrack Agent"
```

### Reiniciar Serviço

```powershell
Restart-Service -Name "TimeTrack Agent"
```

### Verificar Versão

```powershell
# Via arquivo de versão
Get-Content "$env:LOCALAPPDATA\CyriusX\TimeTrack\version.txt"

# Via serviço
(Get-Item "C:\Program Files\TimeTrack\TimeTrack.AgentService.exe").VersionInfo
```

---

## Desinstalação

### Via MSIX

```powershell
# Listar pacote
Get-AppxPackage -Name "CyriusX.TimeTrack*"

# Remover
Get-AppxPackage -Name "CyriusX.TimeTrack*" | Remove-AppxPackage
```

### Via Serviço Windows

```powershell
# Parar serviço
Stop-Service -Name "TimeTrack Agent"

# Remover serviço
sc.exe delete "TimeTrack Agent"

# Remover arquivos
Remove-Item -Recurse -Force "C:\Program Files\TimeTrack"
```

### Limpeza Completa

```powershell
# Remover dados do usuário (CUIDADO: perde histórico local)
Remove-Item -Recurse -Force "$env:LOCALAPPDATA\CyriusX\TimeTrack"
```

---

## Troubleshooting

### Serviço não inicia

1. Verificar se .NET 8 está instalado
2. Verificar permissões da pasta de dados
3. Verificar logs de erro

```powershell
# Verificar logs
Get-Content "$env:LOCALAPPDATA\CyriusX\TimeTrack\logs\agent.log" -Tail 50 | Select-String "Error|Exception"
```

### Database locked

```powershell
# Verificar processos usando o arquivo
handle.exe "timetrack.db"  # Requer Sysinternals Handle

# Solução: reiniciar serviço
Restart-Service -Name "TimeTrack Agent"
```

### Sync não funciona

1. Verificar conectividade com backend
2. Verificar token de autenticação válido
3. Verificar mensagens no outbox

```powershell
# Testar conectividade
Test-NetConnection -ComputerName "api.timetrack.com" -Port 443
```

---

## Contatos

- **Suporte**: suporte@cyriusx.com
- **Documentação**: https://docs.timetrack.com
- **Status**: https://status.timetrack.com
