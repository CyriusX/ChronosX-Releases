# Time Track MVP — Guideline de Execução de Tasks

> **Projeto:** Time Track – MVP (Fase 1)
> **Período:** 09/03/2026 → 01/05/2026
> **Total de tasks:** 63 | **Total de pontos:** ~240 pts
> **Regra geral:** Nunca inicie uma task se alguma das suas dependências ainda não estiver concluída.

---

## Como ler este guideline

- **[CX-NNN]** → identificador da task no Linear
- **Pts** → story points estimados
- **🔴 Urgente / 🟠 High** → prioridade
- Tasks no mesmo "nível" dentro de uma fase podem ser feitas **em paralelo**
- Tasks em níveis diferentes têm dependência **sequencial obrigatória**

---

## FASE 1 — E1: Agent Core

**Target:** 27/03/2026 | Tudo roda localmente, sem backend ainda.

### Nível 1 — Base absoluta (iniciar aqui, nenhuma dependência)

| Task  | Título                                       | Pts | Prioridade |
| ----- | -------------------------------------------- | --- | ---------- |
| CX-80 | Setup: Estrutura do Monorepo e Solution .NET | 3   | 🔴 Urgente |

### Nível 2 — Domínio (depende de CX-80)

| Task  | Título                                               | Pts | Prioridade |
| ----- | ---------------------------------------------------- | --- | ---------- |
| CX-81 | Domain: Entidades e Value Objects do Tracking        | 5   | 🔴 Urgente |
| CX-82 | Domain: Agregado TrackingState e regras Pause/Resume | 4   | 🔴 Urgente |

### Nível 3 — Infra do AgentService (depende de CX-81 + CX-82)

| Task  | Título                                              | Pts | Prioridade |
| ----- | --------------------------------------------------- | --- | ---------- |
| CX-83 | Infra: Windows Service (AgentService) – Scaffolding | 4   | 🔴 Urgente |

### Nível 4 — Adapters e infra de captura (depende de CX-83)

| Task  | Título                                                       | Pts | Prioridade |
| ----- | ------------------------------------------------------------ | --- | ---------- |
| CX-84 | Infra: Adapter IActiveWindowProvider (WinEvent + Polling)    | 6   | 🔴 Urgente |
| CX-85 | Infra: Adapter IIdleDetector (GetLastInputInfo via P/Invoke) | 3   | 🔴 Urgente |
| CX-87 | Infra: SQLite – Setup, Migrations e Repositórios             | 6   | 🔴 Urgente |

### Nível 5 — Use Cases de Application (depende de CX-84 + CX-85 + CX-87)

| Task  | Título                                                 | Pts | Prioridade |
| ----- | ------------------------------------------------------ | --- | ---------- |
| CX-86 | Application: ConsolidateSession (Loop de Persistência) | 5   | 🔴 Urgente |
| CX-88 | Application: Use Cases Pause/Resume Tracking           | 4   | 🔴 Urgente |
| CX-89 | Application: GetLocalDashboard (Relatório Local)       | 4   | 🟠 High    |
| CX-90 | Application: RecordActiveWindow (Catálogo de Apps)     | 3   | 🟠 High    |

### Nível 6 — Infra de notificações e features avançadas do Agent (depende de CX-83 + CX-88)

| Task   | Título                                         | Pts | Prioridade |
| ------ | ---------------------------------------------- | --- | ---------- |
| CX-136 | Infra: Sistema de Notificações Windows (Toast) | 3   | 🟠 High    |

### Nível 7 — Features de UX do Agent (depende de CX-85 + CX-88 + CX-136)

| Task   | Título                                                              | Pts | Prioridade |
| ------ | ------------------------------------------------------------------- | --- | ---------- |
| CX-142 | Agent: Auto-Resume por Detecção de Atividade (Toast de Confirmação) | 4   | 🟠 High    |

### Nível 8 — QA do Agent (depende de todo o E1 acima)

| Task  | Título                                      | Pts | Prioridade |
| ----- | ------------------------------------------- | --- | ---------- |
| CX-91 | QA: Benchmark e Soak Test do Agent (7 dias) | 5   | 🔴 Urgente |

---

## FASE 2 — E2: Sync (Outbox + Ingestão)

**Target:** 10/04/2026 | O Agent começa a se comunicar com o backend.

> ⚠️ Requer E4 iniciado (pelo menos CX-109 + CX-110 + CX-95) para os testes E2E finais.

### Nível 1 — Outbox (depende de CX-87)

| Task  | Título                                         | Pts | Prioridade |
| ----- | ---------------------------------------------- | --- | ---------- |
| CX-92 | Domain/Infra: Outbox Pattern no Agent (SQLite) | 5   | 🔴 Urgente |

### Nível 2 — SyncWorker e HTTP Client (depende de CX-92)

| Task  | Título                                               | Pts | Prioridade |
| ----- | ---------------------------------------------------- | --- | ---------- |
| CX-93 | Application: SyncWorker (Loop Batch + Retry/Backoff) | 5   | 🔴 Urgente |
| CX-94 | Infra: HTTP Sync Client (ISyncTransport)             | 4   | 🔴 Urgente |

### Nível 3 — Device e Token (depende de CX-93 + CX-94)

| Task  | Título                                             | Pts | Prioridade |
| ----- | -------------------------------------------------- | --- | ---------- |
| CX-96 | Backend: RegisterDevice e Heartbeat                | 3   | 🟠 High    |
| CX-97 | Infra: Gerenciamento de Token JWT no Agent (DPAPI) | 3   | 🔴 Urgente |
| CX-98 | Infra: Registro de Erros de Sync e Diagnóstico     | 2   | 🟠 High    |

### Nível 4 — Testes E2E (depende de E2 completo + CX-95 do E4)

| Task  | Título                               | Pts | Prioridade |
| ----- | ------------------------------------ | --- | ---------- |
| CX-99 | Testes E2E: Agent → Cloud → Postgres | 5   | 🔴 Urgente |

---

## FASE 3 — E4: Back-end Cloud

**Target:** 24/04/2026 | Pode ser iniciado em paralelo com E2 a partir do nível 1.

### Nível 1 — Setup da infra (sem dependências)

| Task   | Título                                        | Pts | Prioridade |
| ------ | --------------------------------------------- | --- | ---------- |
| CX-109 | Setup: ASP.NET Core + EF Core + Neon Postgres | 4   | 🔴 Urgente |

### Nível 2 — Auth e RBAC (depende de CX-109)

| Task   | Título                                                     | Pts | Prioridade |
| ------ | ---------------------------------------------------------- | --- | ---------- |
| CX-110 | Backend: Auth – Login, Refresh Token e JWT                 | 5   | 🔴 Urgente |
| CX-111 | Backend: RBAC e Multi-tenancy (org_id em todas as queries) | 4   | 🔴 Urgente |

### Nível 3 — Ingestão, usuários e políticas (depende de CX-110 + CX-111)

| Task   | Título                                                       | Pts | Prioridade |
| ------ | ------------------------------------------------------------ | --- | ---------- |
| CX-95  | Backend: Endpoint de Ingestão Idempotente                    | 5   | 🔴 Urgente |
| CX-114 | Backend: Organizations e Users (CRUD + Convites)             | 4   | 🟠 High    |
| CX-113 | Backend: Políticas (Horário de Trabalho + Exclusões de Apps) | 5   | 🔴 Urgente |

### Nível 4 — Relatórios, Teams e Jobs (depende de CX-111 + CX-113 + CX-114)

| Task   | Título                                                           | Pts | Prioridade |
| ------ | ---------------------------------------------------------------- | --- | ---------- |
| CX-112 | Backend: Relatórios – Daily Summary e Top Apps                   | 5   | 🔴 Urgente |
| CX-115 | Backend: Audit Log Mínimo (MVP)                                  | 3   | 🟠 High    |
| CX-116 | Backend: Rate Limiting e Proteção Básica                         | 3   | 🟠 High    |
| CX-117 | Backend: Workers/Jobs – Retenção, Agregação e Limpeza (Hangfire) | 4   | 🟠 High    |
| CX-135 | Backend + Frontend: Times (Teams) e Granularidade do Gestor      | 5   | 🟠 High    |

### Nível 5 — Focus Score e Políticas de Foco (depende de CX-109 + CX-95 + CX-113)

| Task   | Título                                                   | Pts | Prioridade |
| ------ | -------------------------------------------------------- | --- | ---------- |
| CX-133 | Backend: Tabela focus_sessions + Cálculo do Focus Score  | 4   | 🔴 Urgente |
| CX-137 | Backend: Política de Modo de Foco (Pomodoro / Ultradian) | 3   | 🟠 High    |

### Nível 6 — DevOps e QA do Backend (depende de todo E4 acima)

| Task   | Título                                      | Pts | Prioridade |
| ------ | ------------------------------------------- | --- | ---------- |
| CX-118 | DevOps: Docker + CI/CD para o Backend       | 4   | 🔴 Urgente |
| CX-119 | QA: Testes de Integração com Testcontainers | 4   | 🔴 Urgente |

---

## FASE 4 — E3: UI Desktop (React + WebView2)

**Target:** 17/04/2026 | Pode começar em paralelo com E4 após CX-83 (AgentService pronto).

### Nível 1 — Setup do DesktopHost e React (depende de CX-83)

| Task   | Título                                             | Pts | Prioridade |
| ------ | -------------------------------------------------- | --- | ---------- |
| CX-100 | Setup: DesktopHost .NET + WebView2 + IPC Server    | 5   | 🔴 Urgente |
| CX-101 | Setup: Projeto React (Vite + TypeScript + Zustand) | 3   | 🔴 Urgente |

### Nível 2 — Modo de exibição e Design System (depende de CX-100 + CX-101)

| Task   | Título                                                    | Pts | Prioridade |
| ------ | --------------------------------------------------------- | --- | ---------- |
| CX-141 | DesktopHost: Modo Background vs. Foreground (DisplayMode) | 4   | 🟠 High    |
| CX-132 | Design System: Tokens, Paleta Dark Theme e Componentes    | 3   | 🔴 Urgente |

### Nível 3 — Auth, IPC e Telas base (depende de CX-100 + CX-101 + CX-132 + CX-110)

| Task   | Título                                                | Pts | Prioridade |
| ------ | ----------------------------------------------------- | --- | ---------- |
| CX-106 | Frontend: Auth Flow (Login + Token Cache Seguro)      | 3   | 🔴 Urgente |
| CX-105 | Frontend: Integração IPC Completa (Commands + Events) | 4   | 🔴 Urgente |

### Nível 4 — Dashboard do Colaborador — estrutura (depende de CX-105 + CX-106 + CX-89)

| Task   | Título                                                | Pts | Prioridade |
| ------ | ----------------------------------------------------- | --- | ---------- |
| CX-102 | Frontend: Dashboard do Colaborador (estrutura base)   | 5   | 🔴 Urgente |
| CX-103 | Frontend: Tela de Status e Controle do Agent          | 3   | 🟠 High    |
| CX-104 | Frontend: Tela de Configurações (Horário + Exclusões) | 4   | 🟠 High    |

### Nível 5 — Componentes do Dashboard (depende de CX-102 + CX-132)

| Task   | Título                                                     | Pts | Prioridade |
| ------ | ---------------------------------------------------------- | --- | ---------- |
| CX-127 | Dashboard: Sidebar de Navegação                            | 3   | 🔴 Urgente |
| CX-128 | Dashboard: Cards de Resumo Superior (Tempo + Foco + Timer) | 5   | 🔴 Urgente |
| CX-129 | Dashboard: Timeline de Atividade (Heatmap Horizontal)      | 4   | 🔴 Urgente |
| CX-130 | Dashboard: Cards de Categorias, Apps & Sites e Projetos    | 4   | 🟠 High    |
| CX-131 | Dashboard: Painel Direito (Equipe + Tempo por Projeto)     | 5   | 🟠 High    |

### Nível 6 — Features avançadas de UI (depende de CX-105 + CX-128 + CX-132 + CX-137 + CX-138)

| Task   | Título                                                | Pts | Prioridade |
| ------ | ----------------------------------------------------- | --- | ---------- |
| CX-138 | Agent: FocusModeEngine (Pomodoro + Ultradian + Toast) | 6   | 🟠 High    |
| CX-139 | Frontend: Card Timer com Modo de Foco                 | 5   | 🟠 High    |

### Nível 7 — Admin Panel e Gestor (depende de CX-111 + CX-113 + CX-114 + CX-132 + CX-135)

| Task   | Título                                                      | Pts | Prioridade |
| ------ | ----------------------------------------------------------- | --- | ---------- |
| CX-134 | Frontend: Painel de Administração (Membros + Políticas)     | 5   | 🟠 High    |
| CX-107 | Frontend: Dashboard do Gestor (Web – Relatórios por Equipe) | 5   | 🔴 Urgente |
| CX-108 | Backend: Export CSV (Colaborador e Gestor)                  | 2   | 🟠 High    |

---

## FASE 5 — E5: Observabilidade, Hardening e Piloto

**Target:** 01/05/2026 | Gate final antes do Go/No-Go.

> ⚠️ Só inicie E5 após E1 + E2 + E3 + E4 todos concluídos.

### Nível 1 — Observabilidade (pode rodar em paralelo com E4 final)

| Task   | Título                                                      | Pts | Prioridade |
| ------ | ----------------------------------------------------------- | --- | ---------- |
| CX-120 | Observabilidade: Logs Estruturados com Correlação           | 4   | 🔴 Urgente |
| CX-121 | Observabilidade: Métricas do Agent (CPU, RAM, Sync Backlog) | 3   | 🔴 Urgente |

### Nível 2 — Hardening e Instalador (depende de E1 + E2 + E3 completos)

| Task   | Título                                                        | Pts | Prioridade |
| ------ | ------------------------------------------------------------- | --- | ---------- |
| CX-122 | Hardening: Revisão de Segurança e Privacidade                 | 5   | 🔴 Urgente |
| CX-123 | DevOps: Instalador MSIX do Agent para Windows (+ DisplayMode) | 5   | 🔴 Urgente |

### Nível 3 — Performance e Documentação (depende de CX-122 + CX-123)

| Task   | Título                                                | Pts | Prioridade |
| ------ | ----------------------------------------------------- | --- | ---------- |
| CX-125 | Documentação: Runbooks e README Final                 | 3   | 🟠 High    |
| CX-126 | QA/Performance: Suite Completa (BenchmarkDotNet + k6) | 4   | 🔴 Urgente |

### Nível 4 — Piloto (gate final — depende de CX-122 + CX-123 + CX-125 + CX-126)

| Task   | Título                                             | Pts | Prioridade |
| ------ | -------------------------------------------------- | --- | ---------- |
| CX-124 | Piloto: Setup de 7 Máquinas e Monitoramento 7 Dias | 5   | 🔴 Urgente |

---

## Mapa de dependências críticas (visão rápida)

```
CX-80 → CX-81 → CX-82 → CX-83 ──────────────────────────────────────────────┐
                                  │                                             │
                          CX-84 ──┤                                   CX-100 ──┤
                          CX-85 ──┤→ CX-86 → CX-88 ─────────────────►CX-141  │
                          CX-87 ──┘          │                                 │
                                             │→ CX-136 → CX-142               │
                                             │→ CX-89 ──► UI Dashboard        │
                                             └→ CX-92 → CX-93 → CX-94        │
                                                                    │          │
CX-109 → CX-110 → CX-111 ──────────────────────────────────────────┘          │
                      │→ CX-95 → CX-133                                        │
                      │→ CX-113 → CX-137 → CX-138 → CX-139                   │
                      │→ CX-114 → CX-135 → CX-134                            │
                      └→ CX-112 ──────────────────────────────────────────────┘
                                                              ▼
                                                    E5: CX-122 → CX-123 → CX-124
```

---

## Paralelismo recomendado por sprint

| Sprint | Semana      | Frentes paralelas                                                              |
| ------ | ----------- | ------------------------------------------------------------------------------ |
| 1      | 09–13/03    | E1: CX-80 → CX-81 → CX-82 → CX-83 (sequencial, base de tudo)                   |
| 2      | 16–20/03    | E1: CX-84 + CX-85 + CX-87 (paralelo) · E4: CX-109 já pode iniciar              |
| 3      | 23–27/03    | E1: CX-86 + CX-88 + CX-89 + CX-90 · E4: CX-110 + CX-111                        |
| 4      | 30/03–03/04 | E2: CX-92 → CX-93 + CX-94 · E4: CX-95 + CX-113 + CX-114 · E3: CX-100 + CX-101  |
| 5      | 06–10/04    | E2: CX-96 + CX-97 + CX-98 · E4: CX-112 + CX-135 + CX-133 · E3: CX-132 + CX-141 |
| 6      | 13–17/04    | E2: CX-99 (E2E) · E4: CX-136 + CX-137 · E3: CX-105 + CX-106 → CX-102           |
| 7      | 20–24/04    | E3: CX-127 + CX-128 + CX-129 + CX-130 + CX-131 · E4: CX-118 + CX-119           |
| 8      | 27/04–01/05 | E3: CX-138 + CX-139 + CX-134 + CX-107 · E5: CX-120 + CX-121 + CX-122 + CX-123  |
| Piloto | 04–08/05    | E5: CX-124 (7 dias de piloto com usuários reais)                               |

---

## Checklist de pré-requisitos por fase

### ✅ Para iniciar E2 (Sync)

- [ ] CX-87 concluído (SQLite local funcionando)
- [ ] CX-86 concluído (sessões sendo persistidas)

### ✅ Para iniciar E3 (UI Desktop)

- [ ] CX-83 concluído (AgentService rodando como Windows Service)
- [ ] CX-89 concluído (GetLocalDashboard retornando dados)

### ✅ Para iniciar componentes do Dashboard (CX-127 a CX-131)

- [ ] CX-102 concluído (estrutura base do dashboard)
- [ ] CX-132 concluído (design system com tokens)
- [ ] CX-105 concluído (IPC completo)

### ✅ Para iniciar E5 (Hardening + Piloto)

- [ ] CX-91 concluído (soak test do Agent)
- [ ] CX-99 concluído (E2E sync)
- [ ] CX-119 concluído (testes de integração backend)
- [ ] CX-118 concluído (CI/CD funcionando)

---

_Gerado em 08/03/2026 — Time Track MVP (Fase 1)_
