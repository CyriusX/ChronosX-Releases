# Time Track MVP

Sistema de rastreamento de tempo para Windows Desktop com multi-tenancy B2B/B2C.

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
│   ├── migrations/
│   └── runbooks/
│
├── docs/
├── TimeTrack.sln
└── README.md
```

## Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 20+](https://nodejs.org/) (para UI React)
- [PostgreSQL](https://www.postgresql.org/) (ou Docker)

## Como Executar

### Backend

```bash
# Restaurar dependências
dotnet restore TimeTrack.sln

# Build
dotnet build TimeTrack.sln

# Executar API
cd src/backend/TimeTrack.Api
dotnet run
```

### Agent (Windows Service)

```bash
cd src/agent/TimeTrack.AgentService
dotnet run
```

### Tests

```bash
dotnet test TimeTrack.sln
```

### UI React

```bash
cd src/ui/timetrack-ui
npm install
npm run dev
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

## Padrões Utilizados

- Clean Architecture + DDD
- SOLID Principles
- Composition Pattern
- CQRS (futuro)

## Documentação

- [Fases de Execução](docs/01-fases-execucao.md)
- [Lista de Tarefas](docs/02-lista-tarefas.md)
- [Arquitetura](docs/03-arquitetura.md)
- [Dependências](docs/04-dependencias.md)

## Licença

Privado - CyriusX
