# Runbook: Diagnóstico e Troubleshooting

## Visão Geral

Este runbook cobre diagnóstico de problemas comuns no TimeTrack.

---

## Localização dos Logs

### Agent (Windows Service)

| Local | Descrição |
|-------|-----------|
| `%LOCALAPPDATA%\CyriusX\TimeTrack\logs\` | Logs em arquivo |
| Event Viewer > Application | Windows Event Log (Source: TimeTrack) |

```powershell
# Ver logs do Agent
Get-Content "$env:LOCALAPPDATA\CyriusX\TimeTrack\logs\agent.log" -Tail 100

# Via Event Viewer
Get-WinEvent -LogName Application -FilterXPath "*[System[Provider[@Name='TimeTrack']]]" -MaxEvents 50
```

### Backend (API)

| Ambiente | Local |
|----------|-------|
| Docker | `docker logs timetrack-api` |
| Local | Console output |

```bash
# Ver logs do container
docker logs timetrack-api --tail 100 --follow

# Filtrar erros
docker logs timetrack-api 2>&1 | grep -i "error\|exception"

# Exportar para análise
docker logs timetrack-api > backend_logs_$(date +%Y%m%d).log
```

### Desktop Host (UI)

| Local | Descrição |
|-------|-----------|
| `%LOCALAPPDATA%\CyriusX\TimeTrack\logs\` | Logs do DesktopHost |
| DevTools (F12) | Console do WebView2 |

---

## Investigar Falha de Sync

### Sintomas

- Dados não aparecem no dashboard web
- Mensagens acumulam localmente
- Erros de conexão nos logs

### Passos de Diagnóstico

#### 1. Verificar Conectividade

```powershell
# Testar DNS
nslookup api.timetrack.com

# Testar conexão TCP
Test-NetConnection -ComputerName api.timetrack.com -Port 443

# Testar HTTPS
Invoke-WebRequest -Uri "https://api.timetrack.com/health" -UseBasicParsing
```

#### 2. Verificar Token JWT

```powershell
# Verificar se token existe
Test-Path "$env:LOCALAPPDATA\CyriusX\TimeTrack\tokens.dat"

# Token expirado? Verificar logs
Get-Content "$env:LOCALAPPDATA\CyriusX\TimeTrack\logs\agent.log" | Select-String "token|auth|401"
```

#### 3. Verificar Outbox

```powershell
# Verificar mensagens pendentes
# Requer SQLite CLI ou ferramenta de DB
sqlite3 "$env:LOCALAPPDATA\CyriusX\TimeTrack\timetrack.db" "SELECT COUNT(*) FROM outbox_messages WHERE sent_at IS NULL;"
```

#### 4. Verificar Logs de Sync

```powershell
# Filtrar logs de sync
Get-Content "$env:LOCALAPPDATA\CyriusX\TimeTrack\logs\agent.log" | Select-String "SyncWorker|sync|outbox"
```

### Soluções Comuns

| Problema | Solução |
|----------|---------|
| Sem conectividade | Verificar proxy/firewall |
| Token expirado | Re-autenticar via UI |
| Backend indisponível | Aguardar ou contatar suporte |
| Erro 429 (rate limit) | Aguardar e tentar novamente |

---

## Erros Comuns e Soluções

### 1. "Database locked" (SQLite)

**Causa**: Múltiplas operações de escrita simultâneas.

**Solução**:
```powershell
# Reiniciar o serviço
Restart-Service -Name "TimeTrack Agent"

# Se persistir, verificar processos
# Requer Sysinternals Handle
handle.exe "timetrack.db"
```

**Prevenção**: Verificar se WAL mode está ativo:
```sql
PRAGMA journal_mode;
-- Deve retornar "wal"
```

### 2. "JWT token expired" / 401 Unauthorized

**Causa**: Token de autenticação expirado (padrão: 60 min).

**Solução**:
1. Abrir UI do TimeTrack
2. Fazer login novamente
3. Verificar se sync retoma

**Prevenção**: O sistema deve fazer refresh automático. Se não estiver:
```powershell
# Verificar logs de refresh
Get-Content "$env:LOCALAPPDATA\CyriusX\TimeTrack\logs\agent.log" | Select-String "refresh|token"
```

### 3. "IPC connection refused" / UI não responde

**Causa**: Named Pipe não disponível (serviço não rodando).

**Solução**:
```powershell
# Verificar status do serviço
Get-Service -Name "TimeTrack Agent"

# Se parado, iniciar
Start-Service -Name "TimeTrack Agent"

# Verificar logs de inicialização
Get-Content "$env:LOCALAPPDATA\CyriusX\TimeTrack\logs\agent.log" -Tail 20
```

### 4. "Migration failed" (Backend)

**Causa**: Migration não aplicada ou conflito.

**Solução**:
```bash
# Verificar migrations aplicadas
psql "$DATABASE_URL" -c "SELECT * FROM __EFMigrationsHistory ORDER BY MigrationId;"

# Se migration faltando, aplicar manualmente
dotnet ef database update \
  --project src/backend/TimeTrack.Backend.Infrastructure \
  --startup-project src/backend/TimeTrack.Api
```

### 5. "Hangfire job failed"

**Causa**: Job em background falhou repetidamente.

**Solução**:
1. Acessar `/hangfire` dashboard
2. Verificar jobs com status "Failed"
3. Clicar no job para ver stack trace
4. Corrigir causa raiz
5. Re-executar job manualmente

### 6. "Connection string not found"

**Causa**: Variável de ambiente não configurada.

**Solução**:
```bash
# Verificar variáveis
docker exec timetrack-api env | grep ConnectionStrings

# Configurar se faltando
docker stop timetrack-api
docker run -d --name timetrack-api \
  -e ConnectionStrings__DefaultConnection="..." \
  timetrack-api:latest
```

---

## Ferramentas de Diagnóstico

### Agent

```powershell
# Status completo
Get-Service -Name "TimeTrack Agent"
Get-Process -Name "TimeTrack.AgentService" -ErrorAction SilentlyContinue

# Memória e CPU
Get-Process -Name "TimeTrack.AgentService" | Select-Object CPU, WorkingSet64

# Tamanho do banco
(Get-ChildItem "$env:LOCALAPPDATA\CyriusX\TimeTrack\timetrack.db").Length / 1MB
```

### Backend

```bash
# Health check completo
curl -s https://api.timetrack.com/health | jq .

# Status do container
docker inspect timetrack-api --format='{{.State.Status}}'

# Uso de recursos
docker stats timetrack-api --no-stream

# Conexões ativas
docker exec timetrack-api netstat -an | grep ESTABLISHED | wc -l
```

### Banco de Dados

```sql
-- Conexões ativas
SELECT count(*) FROM pg_stat_activity WHERE datname = 'timetrack';

-- Queries lentas
SELECT query, mean_exec_time, calls
FROM pg_stat_statements
ORDER BY mean_exec_time DESC
LIMIT 10;

-- Tamanho das tabelas
SELECT relname, pg_size_pretty(pg_total_relation_size(relid))
FROM pg_catalog.pg_statio_user_tables
ORDER BY pg_total_relation_size(relid) DESC;
```

---

## Performance

### Agent usando muita CPU

1. Verificar polling interval
2. Verificar se há loop de retry
3. Verificar tamanho do outbox

```powershell
# Logs de CPU alta
Get-Content "$env:LOCALAPPDATA\CyriusX\TimeTrack\logs\agent.log" | Select-String "high cpu|throttle"
```

### Backend lento

1. Verificar queries N+1
2. Verificar índices
3. Verificar conexões pooling

```bash
# Response times
curl -w "Time: %{time_total}s\n" -o /dev/null -s https://api.timetrack.com/health
```

---

## Como Escalar um Problema

### Níveis de Severidade

| Nível | Descrição | Exemplo |
|-------|-----------|---------|
| **P1 - Crítico** | Sistema completamente indisponível | Backend down, todos usuários afetados |
| **P2 - Alto** | Funcionalidade importante indisponível | Sync não funciona, dados perdidos |
| **P3 - Médio** | Problema afeta poucos usuários | Um usuário não consegue logar |
| **P4 - Baixo** | Inconveniente menor | Logs não aparecem |

### Canais de Escalação

| Sev | Canal | SLA |
|-----|-------|-----|
| P1 | PagerDuty / Telefone | 15 min |
| P2 | Slack #timetrack-ops | 1 hora |
| P3 | Slack #timetrack-support | 4 horas |
| P4 | Email suporte@cyriusx.com | 24 horas |

### Informações para Escalar

Ao escalar, fornecer:

1. **Descrição** do problema
2. **Impacto** (quantos usuários/sistemas afetados)
3. **Timeline** (quando começou)
4. **Logs relevantes** (últimas 50 linhas)
5. **Ações já tentadas**
6. **Hostname/versão** do sistema

---

## Checklist de Diagnóstico

Quando algo der errado, siga esta ordem:

- [ ] Verificar se serviços estão rodando
- [ ] Verificar conectividade de rede
- [ ] Verificar logs recentes
- [ ] Verificar health endpoints
- [ ] Verificar espaço em disco
- [ ] Verificar certificados/tokens
- [ ] Verificar variáveis de ambiente
- [ ] Verificar banco de dados (conexão, locks)
- [ ] Verificar jobs em background (Hangfire)
- [ ] Documentar achados

---

## Contatos

- **Suporte Geral**: suporte@cyriusx.com
- **Emergências (P1)**: +55 XX XXXX-XXXX
- **Slack**: #timetrack-ops
- **Status Page**: https://status.timetrack.com
