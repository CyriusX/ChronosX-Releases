# Time Track MVP — Quick Start

## Próximas Ações (FASE 1)

### Task Atual: CX-80 — Setup: Estrutura do Monorepo e Solution .NET

**Objetivo:** Criar a estrutura base do projeto com monorepo, solution .NET e organização de pastas.

**Pontos:** 3 pts

**Dependências:** Nenhuma

---

## Estrutura de Pastas Proposta

```
TimeTracking/
├── docs/                          # Documentação
│   ├── 01-fases-execucao.md
│   ├── 02-lista-tarefas.md
│   ├── 03-arquitetura.md
│   ├── 04-dependencias.md
│   └── README.md
│
├── src/
│   ├── agent/                     # Agent Core (Windows Service)
│   │   ├── TimeTracking.Agent/
│   │   │   ├── Domain/            # Entidades e Value Objects
│   │   │   ├── Application/       # Use Cases
│   │   │   ├── Infrastructure/    # Adapters, SQLite, Windows APIs
│   │   │   └── Service/           # Windows Service Host
│   │   │
│   │   ├── TimeTracking.Agent.Tests/
│   │   └── TimeTracking.Agent.Benchmarks/
│   │
│   ├── desktop/                   # Desktop Host (WebView2)
│   │   ├── TimeTracking.DesktopHost/
│   │   └── TimeTracking.DesktopHost.Tests/
│   │
│   ├── backend/                   # Backend Cloud (ASP.NET Core)
│   │   ├── TimeTracking.Api/
│   │   ├── TimeTracking.Core/
│   │   ├── TimeTracking.Infrastructure/
│   │   └── TimeTracking.Tests/
│   │
│   └── shared/                    # Código compartilhado
│       └── TimeTracking.Shared/
│
├── web/                           # React UI (WebView2)
│   ├── src/
│   │   ├── components/
│   │   ├── screens/
│   │   ├── hooks/
│   │   ├── stores/               # Zustand
│   │   ├── services/             # IPC Client
│   │   └── styles/               # TailwindCSS
│   ├── package.json
│   └── vite.config.ts
│
├── TimeTracking.sln               # Solution principal
├── README.md
├── .gitignore
└── docker-compose.yml             # Para desenvolvimento local
```

---

## Comandos para Iniciar

```bash
# Criar solution
dotnet new sln -n TimeTracking

# Criar projetos do Agent
dotnet new classlib -n TimeTracking.Domain -o src/agent/TimeTracking.Domain
dotnet new classlib -n TimeTracking.Application -o src/agent/TimeTracking.Application
dotnet new classlib -n TimeTracking.Infrastructure -o src/agent/TimeTracking.Infrastructure
dotnet new worker -n TimeTracking.Agent -o src/agent/TimeTracking.Agent
dotnet new xunit -n TimeTracking.Agent.Tests -o src/agent/TimeTracking.Agent.Tests

# Criar projetos do Desktop Host
dotnet new winforms -n TimeTracking.DesktopHost -o src/desktop/TimeTracking.DesktopHost

# Criar projetos do Backend
dotnet new webapi -n TimeTracking.Api -o src/backend/TimeTracking.Api
dotnet new classlib -n TimeTracking.Core -o src/backend/TimeTracking.Core
dotnet new classlib -n TimeTracking.Infrastructure -o src/backend/TimeTracking.Infrastructure

# Adicionar à solution
dotnet sln add src/agent/TimeTracking.Domain
dotnet sln add src/agent/TimeTracking.Application
dotnet sln add src/agent/TimeTracking.Infrastructure
dotnet sln add src/agent/TimeTracking.Agent
dotnet sln add src/agent/TimeTracking.Agent.Tests
dotnet sln add src/desktop/TimeTracking.DesktopHost
dotnet sln add src/backend/TimeTracking.Api
dotnet sln add src/backend/TimeTracking.Core
dotnet sln add src/backend/TimeTracking.Infrastructure

# Criar projeto React
npm create vite@latest web -- --template react-ts
cd web
npm install zustand tailwindcss @tailwindcss/vite
```

---

## Próximas Tasks (Após CX-80)

1. **CX-81** — Domain: Entidades e Value Objects do Tracking (5 pts)
2. **CX-82** — Domain: Agregado TrackingState e regras de Pause/Resume (4 pts)

Essas duas podem ser feitas **em paralelo** após CX-80.

---

## Checklist da Fase 1

- [ ] CX-80: Monorepo Setup
- [ ] CX-81: Domain Entities
- [ ] CX-82: Domain TrackingState
- [ ] CX-86: ConsolidateSession Use Case
- [ ] CX-88: Pause/Resume Use Cases
- [ ] CX-89: GetLocalDashboard Use Case
- [ ] CX-90: RecordActiveWindow Use Case
- [ ] CX-83: Windows Service Scaffolding
- [ ] CX-87: SQLite Setup

---

*Documento gerado em 09/03/2026*
