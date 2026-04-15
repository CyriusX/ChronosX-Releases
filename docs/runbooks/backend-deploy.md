# Runbook: Deploy do Backend TimeTrack

## Visão Geral

O Backend TimeTrack é uma API ASP.NET Core 8 que roda em containers Docker.

| Componente | Tecnologia |
|------------|------------|
| **API** | ASP.NET Core 8 |
| **Database** | PostgreSQL (Neon) |
| **Jobs** | Hangfire |
| **Container** | Docker |

---

## Pré-requisitos

| Requisito | Descrição |
|-----------|-----------|
| Docker | 24.x+ |
| Acesso ao registry | Docker Hub ou privado |
| Acesso ao banco | Connection string Neon |
| Secrets | JWT Secret, API keys |

---

## Ambientes

| Ambiente | URL | Banco |
|----------|-----|-------|
| **Staging** | https://staging-api.timetrack.com | Neon staging |
| **Production** | https://api.timetrack.com | Neon production |

---

## Variáveis de Ambiente

### Obrigatórias

```bash
# Connection String
ConnectionStrings__DefaultConnection=Host=cyriusx.com;Port=4003;Database=chronosx;Username=chronos;Password=chronos;SSL Mode=Disable

# JWT
Jwt__Secret=<256-bit-secret>
Jwt__Issuer=TimeTrack
Jwt__Audience=TimeTrack.Api

# Email (Resend)
Resend__ApiKey=re_xxx
Email__From=noreply@timetrack.com
```

### Opcionais

```bash
# Frontend URL para CORS
Frontend__BaseUrl=https://app.timetrack.com

# Logging
LOG_LEVEL=Information
```

---

## Deploy em Staging

### 1. Preparar Imagem

```bash
# Build da imagem
docker build -f ops/docker/Dockerfile.api -t timetrack-api:staging .

# Tag para registry
docker tag timetrack-api:staging registry.example.com/timetrack-api:staging
```

### 2. Push para Registry

```bash
docker push registry.example.com/timetrack-api:staging
```

### 3. Deploy

```bash
# SSH no servidor de staging
ssh staging.timetrack.com

# Pull da imagem
docker pull registry.example.com/timetrack-api:staging

# Parar container atual
docker stop timetrack-api || true

# Iniciar novo container
docker run -d \
  --name timetrack-api \
  --restart unless-stopped \
  -p 5000:8080 \
  -e ASPNETCORE_ENVIRONMENT=Staging \
  -e ConnectionStrings__DefaultConnection="$STAGING_DB" \
  -e Jwt__Secret="$JWT_SECRET" \
  -e Resend__ApiKey="$RESEND_KEY" \
  registry.example.com/timetrack-api:staging
```

### 4. Verificar

```bash
# Health check
curl https://staging-api.timetrack.com/health

# Logs
docker logs timetrack-api --tail 50
```

---

## Deploy em Produção

### Checklist Pré-Deploy

- [ ] Testes passando no CI
- [ ] Staging validado
- [ ] Migrations testadas em staging
- [ ] Backup do banco realizado
- [ ] Rollback plan documentado

### 1. Backup do Banco

```bash
# Via pg_dump (Neon)
pg_dump "$PRODUCTION_DB" > backup_$(date +%Y%m%d_%H%M%S).sql
```

### 2. Aplicar Migrations (Antes do Deploy)

**IMPORTANTE**: Execute migrations ANTES do deploy para evitar downtime.

```bash
# Gerar script SQL das migrations pendentes
dotnet ef migrations script \
  --project src/backend/TimeTrack.Backend.Infrastructure \
  --startup-project src/backend/TimeTrack.Api \
  --idempotent \
  --output migrations.sql

# Revisar o script
cat migrations.sql

# Aplicar no banco de produção
psql "$PRODUCTION_DB" -f migrations.sql
```

### 3. Deploy Blue-Green (Recomendado)

```bash
# 1. Iniciar novo container (green)
docker run -d \
  --name timetrack-api-green \
  --restart unless-stopped \
  -p 5001:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ConnectionStrings__DefaultConnection="$PRODUCTION_DB" \
  -e Jwt__Secret="$JWT_SECRET" \
  -e Resend__ApiKey="$RESEND_KEY" \
  registry.example.com/timetrack-api:production

# 2. Aguardar healthy
curl --retry 10 --retry-delay 5 http://localhost:5001/health

# 3. Trocar tráfego (nginx/load balancer)
# Atualizar upstream para apontar para porta 5001

# 4. Parar container antigo (blue)
docker stop timetrack-api-blue || true
docker rm timetrack-api-blue || true

# 5. Renomear green para blue
docker rename timetrack-api-green timetrack-api-blue
```

### 4. Verificação Pós-Deploy

```bash
# Health check
curl https://api.timetrack.com/health

# Verificar versão
curl https://api.timetrack.com/api/version

# Verificar logs
docker logs timetrack-api --tail 100

# Verificar Hangfire
curl https://api.timetrack.com/hangfire
```

---

## Rollback

### Identificar Versão Anterior

```bash
# Listar imagens disponíveis
docker images | grep timetrack-api

# Ver containers parados
docker ps -a | grep timetrack-api
```

### Rollback Rápido (Blue-Green)

```bash
# Se ainda temos o container anterior
docker start timetrack-api-blue

# Trocar tráfego de volta
# Atualizar load balancer para porta antiga
```

### Rollback de Migration

```bash
# CUIDADO: Pode causar perda de dados
# Reverter migration manualmente com SQL

# Exemplo: remover tabela adicionada
psql "$PRODUCTION_DB" -c "DROP TABLE IF EXISTS new_table;"
```

---

## Gestão de Secrets

### Desenvolvimento

Secrets em `appsettings.Development.json` (não commitar):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=timetrack;Username=postgres;Password=postgres"
  },
  "Jwt": {
    "Secret": "dev-secret-min-32-characters-long"
  }
}
```

### Produção

**NUNCA** commitar secrets no código.

Opções:
1. **Environment Variables**: Injetadas no container
2. **Azure Key Vault**: Se hospedado no Azure
3. **AWS Secrets Manager**: Se hospedado na AWS
4. **HashiCorp Vault**: Solução agnóstica

```bash
# Exemplo com environment variables
docker run -d \
  --name timetrack-api \
  -e ConnectionStrings__DefaultConnection="$(vault read -field=connection_string secret/timetrack)" \
  -e Jwt__Secret="$(vault read -field=jwt_secret secret/timetrack)" \
  timetrack-api:latest
```

---

## Monitoramento

### Health Endpoints

| Endpoint | Descrição |
|----------|-----------|
| `/health` | Status geral da aplicação |
| `/hangfire` | Dashboard de jobs |

### Logs

```bash
# Ver logs em tempo real
docker logs -f timetrack-api

# Filtrar por nível
docker logs timetrack-api 2>&1 | grep -i error

# Exportar logs
docker logs timetrack-api > logs_$(date +%Y%m%d).log
```

### Métricas Importantes

- Response time p95 < 200ms
- Error rate < 1%
- Hangfire jobs failing = 0
- Database connections < 100

---

## Contatos de Emergência

- **On-Call**: https://pagerduty.com/timetrack
- **Slack**: #timetrack-ops
- **Email**: ops@cyriusx.com
