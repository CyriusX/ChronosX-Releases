# Time Track MVP — Arquitetura e Stack

## Visão Geral da Arquitetura

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              TIME TRACK MVP                                  │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                        WINDOWS DESKTOP APP                           │   │
│  │  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────────┐  │   │
│  │  │   Agent Core    │  │  Focus Engine   │  │    Desktop Host     │  │   │
│  │  │  (Windows Svc)  │  │ (Pomodoro/      │  │   (.NET + WebView2) │  │   │
│  │  │                 │  │  Ultradian)     │  │                     │  │   │
│  │  │  • Tracking     │  │                 │  │  ┌───────────────┐  │  │   │
│  │  │  • Idle Detect  │  │  • Timers       │  │  │  React UI     │  │  │   │
│  │  │  • App Catalog  │  │  • Toasts       │  │  │  (Dashboard)  │  │  │   │
│  │  │  • SQLite       │  │  • Auto-resume  │  │  │  • Zustand    │  │  │   │
│  │  │  • Outbox       │  │                 │  │  │  • Dark Theme │  │  │   │
│  │  └────────┬────────┘  └────────┬────────┘  │  └───────┬───────┘  │  │   │
│  │           │                    │           └──────────┼──────────┘  │   │
│  │           └────────────────────┼──────────────────────┘             │   │
│  │                                │ IPC                                │   │
│  └────────────────────────────────┼────────────────────────────────────┘   │
│                                   │                                         │
│                          ┌────────┴────────┐                                │
│                          │   Sync Worker   │                                │
│                          │  (HTTP/JWT)     │                                │
│                          └────────┬────────┘                                │
│                                   │                                         │
└───────────────────────────────────┼─────────────────────────────────────────┘
                                    │
                                    │ HTTPS
                                    ▼
┌───────────────────────────────────────────────────────────────────────────────┐
│                              CLOUD BACKEND                                     │
│  ┌─────────────────────────────────────────────────────────────────────────┐  │
│  │                      ASP.NET Core API                                    │  │
│  │  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────────┐   │  │
│  │  │  Auth    │ │ Ingestão │ │ Reports  │ │  Admin   │ │   Workers    │   │  │
│  │  │  JWT     │ │ Idempot. │ │ Daily/   │ │  Org/    │ │  Hangfire    │   │  │
│  │  │  Device  │ │          │ │ Top Apps │ │  Users   │ │  Aggregation │   │  │
│  │  └──────────┘ └──────────┘ └──────────┘ └──────────┘ └──────────────┘   │  │
│  │                                                                          │  │
│  │  ┌──────────────────────────────────────────────────────────────────┐   │  │
│  │  │                    Multi-Tenancy (org_id)                         │   │  │
│  │  └──────────────────────────────────────────────────────────────────┘   │  │
│  └─────────────────────────────────────────────────────────────────────────┘  │
│                                       │                                        │
│                                       ▼                                        │
│  ┌─────────────────────────────────────────────────────────────────────────┐  │
│  │                        Neon Postgres                                      │  │
│  │  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────────┐   │  │
│  │  │  Users   │ │ Sessions │ │  Events  │ │ Policies │ │ Focus Scores │   │  │
│  │  │  Orgs    │ │ Devices  │ │  Apps    │ │  Teams   │ │  Categories  │   │  │
│  │  └──────────┘ └──────────┘ └──────────┘ └──────────┘ └──────────────┘   │  │
│  └─────────────────────────────────────────────────────────────────────────┘  │
│                                                                                 │
│  ┌─────────────────────────────────────────────────────────────────────────┐  │
│  │                        Observabilidade                                   │  │
│  │         Logs Estruturados │ Métricas │ Correlação                       │  │
│  └─────────────────────────────────────────────────────────────────────────┘  │
│                                                                                 │
└───────────────────────────────────────────────────────────────────────────────┘
```

---

## Stack Tecnológica

### Desktop App (Agent)

| Camada | Tecnologia | Propósito |
|--------|------------|-----------|
| **Agent Service** | C# / .NET 8 | Windows Service de tracking |
| **Desktop Host** | C# / .NET 8 + WebView2 | Container para UI React |
| **UI Frontend** | React 18 + Vite + TypeScript | Interface do usuário |
| **State Management** | Zustand | Gerenciamento de estado |
| **Styling** | TailwindCSS + Dark Theme | Design System |
| **Local DB** | SQLite + EF Core | Persistência local |
| **Interop** | IPC (Named Pipes / TCP) | Comunicação Agent ↔ UI |

### Backend Cloud

| Camada | Tecnologia | Propósito |
|--------|------------|-----------|
| **API** | ASP.NET Core 8 | REST API |
| **ORM** | EF Core 8 | Mapeamento objeto-relacional |
| **Database** | Neon Postgres | Banco de dados serverless |
| **Auth** | JWT + Refresh Token | Autenticação |
| **Jobs** | Hangfire | Background workers |
| **Container** | Docker | Containerização |
| **CI/CD** | GitHub Actions | Pipeline de deploy |

---

## Padrões Arquiteturais

### Clean Architecture + DDD

```
┌─────────────────────────────────────────────────────────────┐
│                    PRESENTATION LAYER                        │
│         (React UI / WebView2 / ASP.NET Controllers)         │
├─────────────────────────────────────────────────────────────┤
│                   APPLICATION LAYER                          │
│              (Use Cases / Application Services)              │
├─────────────────────────────────────────────────────────────┤
│                     DOMAIN LAYER                             │
│       (Entities / Value Objects / Aggregates / Rules)        │
├─────────────────────────────────────────────────────────────┤
│                  INFRASTRUCTURE LAYER                        │
│    (Adapters / Repositories / External Services / DB)        │
└─────────────────────────────────────────────────────────────┘
```

### SOLID Principles

- **S**ingle Responsibility: Cada classe tem uma única responsabilidade
- **O**pen/Closed: Aberto para extensão, fechado para modificação
- **L**iskov Substitution: Subtipos substituíveis por seus tipos base
- **I**nterface Segregation: Interfaces específicas e coesas
- **D**ependency Inversion: Depender de abstrações, não de implementações

### Composition Pattern

- Preferir composição sobre herança
- Injeção de dependência em todos os níveis
- Adapters para serviços externos

---

## Fluxo de Dados

### Tracking Flow

```
1. User Activity
       ↓
2. IActiveWindowProvider (WinEvent/Polling)
       ↓
3. IIdleDetector (GetLastInputInfo)
       ↓
4. RecordActiveWindow Use Case
       ↓
5. ConsolidateSession Use Case
       ↓
6. SQLite (Local DB)
       ↓
7. Outbox Pattern
       ↓
8. SyncWorker (Batch + Retry)
       ↓
9. Backend API (Ingestão)
       ↓
10. Neon Postgres (Cloud DB)
```

### Auth Flow

```
1. Login UI (React)
       ↓
2. IPC → DesktopHost
       ↓
3. HTTP → Backend /auth/login
       ↓
4. JWT Token + Refresh Token
       ↓
5. DPAPI Storage (Secure)
       ↓
6. Auto-refresh antes de expirar
```

---

## Multi-Tenancy

```sql
-- Todas as queries são filtradas por org_id
SELECT * FROM sessions WHERE org_id = @org_id;
SELECT * FROM users WHERE org_id = @org_id;
SELECT * FROM policies WHERE org_id = @org_id;
```

### Estrutura de Organização

```
Organization
├── Teams
│   ├── Members (Users)
│   └── Policies (Horário, Exclusões, Focus Mode)
├── Admin
│   └── Gestão de Membros
└── Settings
    └── Categorização de Apps (Override)
```

---

## Segurança

| Aspecto | Implementação |
|---------|---------------|
| **Auth** | JWT + Refresh Token |
| **Token Storage** | DPAPI (Windows Protected) |
| **API Security** | Rate Limiting + HTTPS |
| **Data Isolation** | org_id em todas as queries |
| **Audit** | Log de ações sensíveis |
| **Privacy** | Dados pessoais criptografados |

---

## Performance Targets

| Métrica | Target |
|---------|--------|
| Agent CPU | < 1% idle |
| Agent RAM | < 50 MB |
| Sync Latency | < 5s batch |
| API Response | < 200ms p95 |
| DB Query | < 100ms p95 |

---

*Documento gerado em 09/03/2026*
