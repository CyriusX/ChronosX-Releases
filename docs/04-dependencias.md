# Time Track MVP — Matriz de Dependências

## Grafo de Dependências

```
CX-80 (Monorepo Setup)
├── CX-81 (Domain Entities)
├── CX-82 (Domain TrackingState)
│   ├── CX-86 (ConsolidateSession)
│   ├── CX-88 (Pause/Resume)
│   ├── CX-89 (GetLocalDashboard)
│   └── CX-90 (RecordActiveWindow)
│
├── CX-83 (Windows Service)
│   ├── CX-84 (ActiveWindow Provider)
│   ├── CX-85 (Idle Detector)
│   │   ├── CX-142 (Auto-Resume)
│   │   └── CX-138 (FocusModeEngine)
│   │
│   ├── CX-136 (Toast Notifications)
│   │   ├── CX-142 (Auto-Resume)
│   │   └── CX-138 (FocusModeEngine)
│   │
│   ├── CX-100 (DesktopHost)
│   │   ├── CX-101 (React Setup)
│   │   ├── CX-140 (Display Mode)
│   │   └── CX-141 (DesktopHost Mode)
│   │
│   └── CX-91 (QA Soak Test)
│
└── CX-87 (SQLite Setup)
    ├── CX-92 (Outbox Pattern)
    │   ├── CX-93 (SyncWorker)
    │   └── CX-94 (HTTP Client)
    │       ├── CX-96 (ActivateDevice)
    │       ├── CX-97 (JWT Token)
    │       ├── CX-98 (Error Registry)
    │       └── CX-99 (E2E Tests)
    │
    └── CX-89 (GetLocalDashboard)

CX-109 (Backend Setup)
├── CX-110 (Auth)
│   ├── CX-106 (Frontend Auth)
│   └── CX-111 (RBAC/Multi-tenancy)
│       ├── CX-95 (Ingestion Endpoint)
│       ├── CX-113 (Policies)
│       ├── CX-114 (Organizations/Users)
│       ├── CX-115 (Audit Log)
│       ├── CX-116 (Rate Limiting)
│       ├── CX-117 (Workers/Jobs)
│       ├── CX-112 (Reports)
│       ├── CX-135 (Teams)
│       └── CX-143 (Categorization)
│
├── CX-133 (Focus Score)
├── CX-137 (Focus Mode Policy)
└── CX-108 (Export CSV)

CX-101 (React Setup)
├── CX-132 (Design System)
│   ├── CX-102 (Dashboard)
│   │   ├── CX-127 (Sidebar)
│   │   ├── CX-128 (Cards Resumo)
│   │   ├── CX-129 (Timeline)
│   │   ├── CX-130 (Categorias)
│   │   └── CX-131 (Painel Direito)
│   │
│   └── CX-134 (Admin Panel)
│       └── CX-144 (Categorização UI)
│
├── CX-105 (IPC Integration)
├── CX-106 (Auth Flow)
│   └── CX-145 (Password Change)
├── CX-103 (Status Screen)
├── CX-104 (Settings Screen)
└── CX-146 (Register Screen)

CX-107 (Gestor Dashboard)
├── CX-111 (RBAC)
├── CX-113 (Policies)
├── CX-114 (Organizations)
└── CX-135 (Teams)

CX-139 (Focus Timer Card)
├── CX-105 (IPC)
├── CX-128 (Cards)
└── CX-137 (Focus Mode Policy)

Fases 1-7 Completas
├── CX-120 (Logs)
├── CX-121 (Metrics)
├── CX-122 (Hardening)
├── CX-118 (Docker/CI)
├── CX-123 (MSIX)
├── CX-119 (Integration Tests)
├── CX-126 (Performance Suite)
├── CX-125 (Documentation)
└── CX-124 (Pilot)
```

---

## Dependências por Task

### FASE 1 — Fundação & Agent Core

| Task | Depende de |
|------|------------|
| CX-80 | - |
| CX-81 | CX-80 |
| CX-82 | CX-80 |
| CX-86 | CX-81, CX-82 |
| CX-88 | CX-81, CX-82 |
| CX-89 | CX-81, CX-82 |
| CX-90 | CX-81, CX-82 |
| CX-83 | CX-86 |
| CX-87 | CX-83 |

### FASE 2 — Infraestrutura do Agent

| Task | Depende de |
|------|------------|
| CX-84 | CX-83 |
| CX-85 | CX-83 |
| CX-136 | CX-83 |
| CX-142 | CX-85, CX-88, CX-136 |
| CX-138 | CX-85, CX-88, CX-136 |
| CX-91 | Toda Fase 1 + Fase 2 |

### FASE 3 — Sync & Conectividade

| Task | Depende de |
|------|------------|
| CX-92 | CX-87 |
| CX-93 | CX-92 |
| CX-94 | CX-92 |
| CX-96 | CX-93, CX-94 |
| CX-97 | CX-93, CX-94 |
| CX-98 | CX-93, CX-94 |
| CX-99 | Fase 3 completa + CX-95 |

### FASE 4 — Backend Cloud - Core

| Task | Depende de |
|------|------------|
| CX-109 | - |
| CX-110 | CX-109 |
| CX-111 | CX-110 |
| CX-95 | CX-111 |
| CX-114 | CX-111 |
| CX-113 | CX-111 |
| CX-115 | CX-111 |
| CX-116 | CX-111 |
| CX-117 | CX-111 |

### FASE 5 — Backend Cloud - Features

| Task | Depende de |
|------|------------|
| CX-112 | CX-111, CX-113 |
| CX-133 | CX-109, CX-95, CX-113 |
| CX-137 | CX-109, CX-95, CX-113 |
| CX-135 | CX-111, CX-113, CX-114 |
| CX-143 | CX-111, CX-113 |
| CX-108 | CX-111, CX-112 |

### FASE 6 — UI Desktop - Fundação

| Task | Depende de |
|------|------------|
| CX-100 | CX-83 |
| CX-101 | CX-83 |
| CX-140 | CX-100 |
| CX-141 | CX-100 |
| CX-132 | CX-101 |
| CX-105 | CX-100, CX-101, CX-132, CX-110 |
| CX-106 | CX-100, CX-101, CX-132, CX-110 |
| CX-103 | CX-105, CX-106 |
| CX-104 | CX-105, CX-106 |
| CX-145 | CX-106 |

### FASE 7 — UI Desktop - Dashboard

| Task | Depende de |
|------|------------|
| CX-102 | CX-105, CX-106, CX-89 |
| CX-127 | CX-102, CX-132 |
| CX-128 | CX-102, CX-132 |
| CX-129 | CX-102, CX-132 |
| CX-130 | CX-102, CX-132 |
| CX-131 | CX-102, CX-132 |
| CX-139 | CX-105, CX-128, CX-137 |
| CX-134 | CX-111, CX-113, CX-114, CX-132, CX-135 |
| CX-144 | CX-134, CX-143 |
| CX-107 | CX-111, CX-113, CX-114, CX-132, CX-135 |
| CX-146 | CX-114 |

### FASE 8 — Observabilidade & DevOps

| Task | Depende de |
|------|------------|
| CX-120 | Fases 1-7 |
| CX-121 | Fases 1-7 |
| CX-122 | Fases 1-7 |
| CX-118 | Fase 4, Fase 5 |
| CX-123 | Fase 1, Fase 2, Fase 6 |
| CX-119 | Fase 4, Fase 5 |
| CX-126 | Fases 1-7 |
| CX-125 | Fases 1-7 |

### FASE 9 — Piloto

| Task | Depende de |
|------|------------|
| CX-124 | Fases 1-8 |

---

## Caminho Crítico

```
CX-80 → CX-81 → CX-86 → CX-83 → CX-87 → CX-92 → CX-93 → CX-99 → FASE 8 → CX-124
     → CX-82 ─┘
```

**Duração do Caminho Crítico:** ~8 semanas

---

## Paralelismo Possível

```
Semana 1-2:  FASE 1 (sequencial)
Semana 2-3:  FASE 2 + início FASE 4 (CX-109 pode começar)
Semana 3-4:  FASE 3 + FASE 4 + início FASE 6 (CX-100, CX-101)
Semana 4-5:  FASE 5 + FASE 6 (paralelo)
Semana 5-6:  FASE 7
Semana 6-7:  FASE 8
Semana 7-8:  FASE 9
```

---

*Documento gerado em 09/03/2026*
