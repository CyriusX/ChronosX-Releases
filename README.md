# Time Track MVP

Sistema de rastreamento de tempo para Windows Desktop com multi-tenancy B2B/B2C.

## Visão Geral

O TimeTrack é uma solução completa para rastreamento de tempo de trabalho, composta por:
- **Agent**: Windows Service que roda em background coletando dados de uso
- **Desktop Host**: Aplicação desktop com UI React em WebView2
- **Backend**: API ASP.NET Core para sincronização e relatórios em cloud

A arquitetura foi desenhada para funcionar **offline-first**, com sincronização automática quando conectividade retorna.

## Arquitetura

```
┌─────────────────────────────────────────────────────────────┐
│                    WINDOWS DESKTOP APP                       │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────┐  │
│  │   Agent Core    │  │  Focus Engine   │  │  Desktop    │  │
│  │  (Windows Svc)  │  │ (Pomodoro/      │  │  Host       │  │
│  │                 │  │  Ultradian)     │  │  (.NET +    │  │
│  │  • Tracking     │  │                 │  │  WebView2)  │  │
│  │  • Idle Detect  │  │  • Timers       │  │             │  │
│  │  • SQLite       │  │  • Toasts       │  │  React UI   │  │
│  └─────────────────┘  └─────────────────┘  └─────────────┘  │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                     CLOUD BACKEND                            │
│  ASP.NET Core 8 + EF Core + Neon Postgres + Hangfire        │
│  • JWT Auth + Multi-tenancy                                 │
│  • Ingestão Idempotente                                     │
│  • Relatórios + Focus Score                                 │
└─────────────────────────────────────────────────────────────┘
```

## Pré-requisitos

| Requisito | Versão | Como Verificar |
|-----------|--------|----------------|
| Windows | 10+ (Build 17763+) | `winver` |
| .NET SDK | 8.0.x | `dotnet --version` |
| Node.js | 20.x LTS | `node --version` |
| PostgreSQL | 16.x (ou Docker) | `psql --version` |
| Docker | 24.x+ (opcional) | `docker --version` |

## Setup Rápido (< 30 min)

### 1. Clonar e Restaurar Dependências

```bash
# Clonar repositório
git clone https://github.com/cyriusx/timetrack.git
cd timetrack

# Restaurar dependências .NET
dotnet restore TimeTrack.sln
```

### 2. Configurar Banco de Dados

**Opção A: Docker (Recomendado para dev)**

```bash
# Iniciar PostgreSQL
docker-compose up -d postgres

# Aguardar banco ficar pronto
docker-compose exec postgres pg_isready
```

**Opção B: Neon (Cloud)**

1. Criar conta em [neon.tech](https://neon.tech)
2. Criar projeto `timetrack`
3. Copiar connection string

### 3. Configurar Variáveis de Ambiente

Criar `src/backend/TimeTrack.Api/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=timetrack;Username=postgres;Password=postgres"
  },
  "Jwt": {
    "Secret": "dev-secret-key-must-be-at-least-32-characters-long-for-security"
  },
  "Resend": {
    "ApiKey": "re_dev_xxx"
  }
}
```

### 4. Aplicar Migrations

```bash
cd src/backend/TimeTrack.Api
dotnet ef database update \
  --project ../TimeTrack.Backend.Infrastructure
```

### 5. Iniciar Backend

```bash
# Terminal 1: Backend API
cd src/backend/TimeTrack.Api
dotnet run
```

Backend disponível em:
- API: http://localhost:5000
- Swagger: http://localhost:5000/swagger
- Health: http://localhost:5000/health
- Hangfire: http://localhost:5000/hangfire

### 6. Iniciar Agent

```bash
# Terminal 2: Agent Service
cd src/agent/TimeTrack.AgentService
dotnet run
```

### 7. Iniciar UI

```bash
# Terminal 3: React UI
cd src/ui/timetrack-ui
npm install
npm run dev
```

UI disponível em: http://localhost:5173

### 8. Iniciar Desktop Host (Opcional)

```bash
# Terminal 4: Desktop Host
cd src/agent/TimeTrack.DesktopHost
dotnet run
```

## Testes

### Testes Unitários e de Integração

```bash
# Rodar todos os testes
dotnet test TimeTrack.sln

# Rodar com coverage
dotnet test TimeTrack.sln --collect:"XPlat Code Coverage"

# Rodar testes específicos
dotnet test --filter "FullyQualifiedName~FocusMode"
```

### Testes do Agent

```bash
dotnet test src/agent/TimeTrack.Agent.Tests
```

### Testes do Backend

```bash
dotnet test src/backend/TimeTrack.Backend.Tests
```

### Testes da UI

```bash
cd src/ui/timetrack-ui
npm run test           # Rodar uma vez
npm run test:watch     # Modo watch
npm run test:coverage  # Com coverage
```

## Estrutura do Projeto

```
TimeTracking/
├── src/
│   ├── agent/                    # Agent (Windows Service)
│   │   ├── TimeTrack.Agent.Domain/
│   │   ├── TimeTrack.Agent.Application/
│   │   ├── TimeTrack.Agent.Infrastructure/
│   │   ├── TimeTrack.Agent.Contracts/
│   │   ├── TimeTrack.AgentService/
│   │   ├── TimeTrack.DesktopHost/
│   │   └── TimeTrack.Agent.Tests/
│   │
│   ├── backend/                  # Backend Cloud
│   │   ├── TimeTrack.Api/
│   │   ├── TimeTrack.Backend.Domain/
│   │   ├── TimeTrack.Backend.Application/
│   │   ├── TimeTrack.Backend.Infrastructure/
│   │   └── TimeTrack.Backend.Tests/
│   │
│   └── ui/                       # React UI (WebView2)
│       └── timetrack-ui/
│
├── shared/
│   ├── TimeTrack.SharedKernel/
│   └── TimeTrack.Protocol/
│
├── ops/
│   ├── docker/
│   └── runbooks/
│
├── docs/
│   ├── runbooks/                 # Runbooks operacionais
│   └── adr/                      # Architecture Decision Records
│
├── TimeTrack.sln
└── README.md
```

## Stack Tecnológica

| Camada | Tecnologia |
|--------|------------|
| Agent | .NET 8 + SQLite |
| Desktop Host | .NET 8 + WebView2 |
| UI | React 18 + Vite + Zustand + TailwindCSS |
| Backend | ASP.NET Core 8 + EF Core |
| Database | Neon Postgres |
| Auth | JWT + Refresh Token |
| Jobs | Hangfire |
| Logging | Serilog |

## Padrões Utilizados

- **Clean Architecture** + **DDD**: Separação clara de responsabilidades
- **SOLID Principles**: Código manutenível e extensível
- **Composition Pattern**: Preferir composição sobre herança
- **Outbox Pattern**: Sync confiável com backend

## Documentação

### Runbooks Operacionais

- [Instalação do Agent](docs/runbooks/agent-install.md) - Como instalar, configurar e desinstalar o Agent
- [Deploy do Backend](docs/runbooks/backend-deploy.md) - Deploy em staging/produção, migrations, rollback
- [Troubleshooting](docs/runbooks/troubleshooting.md) - Diagnóstico de problemas comuns

### Architecture Decision Records (ADRs)

- [ADR-0001: Clean Architecture + DDD](docs/adr/0001-clean-architecture-ddd.md)
- [ADR-0002: SQLite para Armazenamento Local](docs/adr/0002-sqlite-local-storage.md)
- [ADR-0003: IPC via Named Pipes](docs/adr/0003-ipc-named-pipes.md)
- [ADR-0004: Outbox Pattern para Sync](docs/adr/0004-outbox-pattern-sync.md)
- [ADR-0005: Hangfire para Background Jobs](docs/adr/0005-hangfire-background-jobs.md)

### Documentação Adicional

- [Fases de Execução](docs/01-fases-execucao.md)
- [Lista de Tarefas](docs/02-lista-tarefas.md)
- [Arquitetura Detalhada](docs/03-arquitetura.md)
- [Dependências](docs/04-dependencias.md)

## Docker

### Desenvolvimento Local

```bash
# Subir PostgreSQL
docker-compose up -d postgres

# Subir stack completa
docker-compose up -d
```

### Build da Imagem

```bash
docker build -f ops/docker/Dockerfile.api -t timetrack-api:latest .
```

## CI/CD

O projeto utiliza GitHub Actions para CI:

- **Build**: Compilação em Windows
- **Test**: Todos os testes unitários e de integração
- **Trigger**: Push para `main` e Pull Requests

Ver: [.github/workflows/ci.yml](.github/workflows/ci.yml)

## Licença

Privado - CyriusX

---

*Última atualização: Março 2026*
