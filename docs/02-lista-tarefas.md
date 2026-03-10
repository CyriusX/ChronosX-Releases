# Time Track MVP — Lista Completa de Tarefas

> **Total:** 67 tarefas | **Pontos:** 277 pts

---

## Por Milestone Original

### E1 — Agent Core (15 tasks, 65 pts)

| ID | Título | Pontos | Status | Fase |
|----|--------|--------|--------|------|
| CX-80 | Setup: Estrutura do Monorepo e Solution .NET | 3 | Backlog | 1 |
| CX-81 | Domain: Entidades e Value Objects do Tracking | 5 | Backlog | 1 |
| CX-82 | Domain: Agregado TrackingState e regras de Pause/Resume | 4 | Backlog | 1 |
| CX-83 | Infra: Windows Service (AgentService) – Scaffolding | 4 | Backlog | 1 |
| CX-84 | Infra: Adapter IActiveWindowProvider (WinEvent + Fallback Polling) | 6 | Backlog | 2 |
| CX-85 | Infra: Adapter IIdleDetector (GetLastInputInfo via P/Invoke) | 3 | Backlog | 2 |
| CX-86 | Application: Use Case ConsolidateSession (Loop de Persistência) | 5 | Backlog | 1 |
| CX-87 | Infra: SQLite – Setup, Migrations e Repositórios | 6 | Backlog | 1 |
| CX-88 | Application: Use Cases Pause/Resume Tracking | 4 | Backlog | 1 |
| CX-89 | Application: Use Case GetLocalDashboard (Relatório Local) | 4 | Backlog | 1 |
| CX-90 | Application: Use Case RecordActiveWindow (Catálogo de Apps) | 3 | Backlog | 1 |
| CX-91 | QA: Benchmark e Soak Test do Agent (7 dias) | 5 | Backlog | 2 |
| CX-136 | Infra: Sistema de Notificações Windows (Toast) | 3 | Backlog | 2 |
| CX-138 | Agent: FocusModeEngine (Pomodoro + Ciclo Ultradian + Toast) | 6 | Backlog | 2 |
| CX-142 | Agent: Auto-Resume por Detecção de Atividade (Toast de Confirmação) | 4 | Backlog | 2 |

---

### E2 — Sync (8 tasks, 32 pts)

| ID | Título | Pontos | Status | Fase |
|----|--------|--------|--------|------|
| CX-92 | Domain/Infra: Outbox Pattern no Agent (SQLite) | 5 | Backlog | 3 |
| CX-93 | Application: SyncWorker (Loop Batch + Retry/Backoff) | 5 | Backlog | 3 |
| CX-94 | Infra: HTTP Sync Client (ISyncTransport) | 4 | Backlog | 3 |
| CX-95 | Backend: Endpoint de Ingestão Idempotente | 5 | Backlog | 4 |
| CX-96 | Backend: ActivateDevice e Heartbeat | 3 | Backlog | 3 |
| CX-97 | Infra: Token JWT no Agent (DPAPI + Refresh Automático + Desativação) | 3 | Backlog | 3 |
| CX-98 | Infra: Registro de Erros de Sync e Diagnóstico | 2 | Backlog | 3 |
| CX-99 | Testes E2E: Agent → Cloud → Postgres | 5 | Backlog | 3 |

---

### E3 — UI Desktop (22 tasks, 89 pts)

| ID | Título | Pontos | Status | Fase |
|----|--------|--------|--------|------|
| CX-100 | Setup: DesktopHost .NET + WebView2 + IPC Server | 5 | Backlog | 6 |
| CX-101 | Setup: Projeto React (Vite + TypeScript + Zustand) | 3 | Backlog | 6 |
| CX-102 | Frontend: Dashboard do Colaborador (Top Apps + Status) | 5 | Backlog | 7 |
| CX-103 | Frontend: Tela de Status e Controle do Agent | 3 | Backlog | 6 |
| CX-104 | Frontend: Tela de Configurações (Horário + Exclusões) | 4 | Backlog | 6 |
| CX-105 | Frontend: Integração IPC Completa (Commands + Events + Auth) | 4 | Backlog | 6 |
| CX-106 | Frontend: Auth Flow – Login, DPAPI e Troca de Senha Obrigatória | 3 | Backlog | 6 |
| CX-107 | Frontend: Dashboard do Gestor (Web – Relatórios por Equipe) | 5 | Backlog | 7 |
| CX-108 | Backend: Export CSV (Colaborador e Gestor) | 2 | Backlog | 5 |
| CX-127 | Dashboard: Sidebar de Navegação | 3 | Backlog | 7 |
| CX-128 | Dashboard: Cards de Resumo Superior (Tempo + Foco + Timer) | 5 | Backlog | 7 |
| CX-129 | Dashboard: Timeline de Atividade (Heatmap Horizontal) | 4 | Backlog | 7 |
| CX-130 | Dashboard: Cards de Categorias, Apps & Sites e Projetos | 4 | Backlog | 7 |
| CX-131 | Dashboard: Painel Direito (Equipe Agora + Tempo por Projeto) | 5 | Backlog | 7 |
| CX-132 | Design System: Tokens, Paleta Dark Theme e Componentes Primitivos | 3 | Backlog | 6 |
| CX-134 | Frontend: Painel de Administração (Membros + Políticas) | 5 | Backlog | 7 |
| CX-139 | Frontend: Card Timer com Modo de Foco (Pomodoro / Ciclo Ultradian) | 5 | Backlog | 7 |
| CX-140 | Agent + DesktopHost: Modo de Exibição (Background/Foreground) | 5 | Backlog | 6 |
| CX-141 | DesktopHost: Modo de Exibição por DisplayMode (Background vs Foreground) | 4 | Backlog | 6 |
| CX-144 | Frontend Admin: Aba de Categorização de Apps/Sites (Override da Org) | 4 | Backlog | 7 |
| CX-145 | Frontend: Tela de Troca de Senha Obrigatória (Primeiro Acesso) | 4 | Backlog | 6 |
| CX-146 | Frontend Web: Tela de Registro – Criar Organização (B2B e B2C) | 4 | Backlog | 7 |

---

### E4 — Backend Cloud (15 tasks, 62 pts)

| ID | Título | Pontos | Status | Fase |
|----|--------|--------|--------|------|
| CX-109 | Setup: ASP.NET Core + EF Core + Neon Postgres | 4 | Backlog | 4 |
| CX-110 | Backend: Auth – Login, Refresh Token, JWT e Ativação de Device | 5 | Backlog | 4 |
| CX-111 | Backend: RBAC e Multi-tenancy (org_id em todas as queries) | 4 | Backlog | 4 |
| CX-112 | Backend: Relatórios – Daily Summary e Top Apps | 5 | Backlog | 5 |
| CX-113 | Backend: Políticas (Horário de Trabalho + Exclusões de Apps) | 5 | Backlog | 4 |
| CX-114 | Backend: Organizations, Users, Convites e Criação de Conta (B2B/B2C) | 4 | Backlog | 4 |
| CX-115 | Backend: Audit Log Mínimo (MVP) | 3 | Backlog | 4 |
| CX-116 | Backend: Rate Limiting e Proteção Básica | 3 | Backlog | 4 |
| CX-117 | Backend: Workers/Jobs – Retenção, Agregação e Limpeza (Hangfire) | 4 | Backlog | 4 |
| CX-118 | DevOps: Docker + CI/CD para o Backend | 4 | Backlog | 8 |
| CX-119 | QA: Testes de Integração com Testcontainers (Suite Completa) | 4 | Backlog | 8 |
| CX-133 | Backend: focus_sessions + Cálculo do Focus Score | 4 | Backlog | 5 |
| CX-135 | Backend + Frontend: Times (Teams) e Granularidade de Acesso do Gestor | 5 | Backlog | 5 |
| CX-137 | Backend: Política de Modo de Foco (Pomodoro / Ciclo Ultradian) | 3 | Backlog | 5 |
| CX-143 | Backend: Sistema de Categorização de Apps/Sites (Lista Global + Override) | 5 | Backlog | 5 |

---

### E5 — Observabilidade (7 tasks, 29 pts)

| ID | Título | Pontos | Status | Fase |
|----|--------|--------|--------|------|
| CX-120 | Observabilidade: Logs Estruturados com Correlação (Agent + Backend) | 4 | Backlog | 8 |
| CX-121 | Observabilidade: Métricas do Agent (CPU, RAM, Sync Backlog) | 3 | Backlog | 8 |
| CX-122 | Hardening: Revisão de Segurança e Privacidade | 5 | Backlog | 8 |
| CX-123 | DevOps: Instalador MSIX do Agent para Windows | 5 | Backlog | 8 |
| CX-124 | Piloto: Setup de 7 Máquinas e Monitoramento 7 Dias | 5 | Backlog | 9 |
| CX-125 | Documentação: Runbooks e README Final | 3 | Backlog | 8 |
| CX-126 | QA/Performance: Suite Completa (BenchmarkDotNet + k6) | 4 | Backlog | 8 |

---

## Resumo por Fase de Execução

| Fase | Nome | Tasks | Pontos |
|------|------|-------|--------|
| 1 | Fundação & Agent Core | 11 | 44 |
| 2 | Infraestrutura do Agent | 8 | 35 |
| 3 | Sync & Conectividade | 8 | 32 |
| 4 | Backend Cloud - Core | 9 | 38 |
| 5 | Backend Cloud - Features | 6 | 24 |
| 6 | UI Desktop - Fundação | 10 | 40 |
| 7 | UI Desktop - Dashboard | 12 | 49 |
| 8 | Observabilidade & DevOps | 7 | 29 |
| 9 | Piloto & Validação | 1 | 5 |
| **TOTAL** | | **67** | **277** |

---

*Documento gerado em 09/03/2026*
