# Time Track MVP — Fases de Execução

> **Projeto:** Time Track – MVP (Fase 1)
> **Período:** 09/03/2026 → 01/05/2026
> **Total de Tasks:** 67 | **Total de Pontos:** 277 pts

---

## Visão Geral das Fases

| Fase | Nome | Tasks | Pontos | Duração | Target |
|------|------|-------|--------|---------|--------|
| 1 | Fundação & Agent Core | 11 | 44 pts | Semana 1-2 | 20/03 |
| 2 | Infraestrutura do Agent | 8 | 35 pts | Semana 2-3 | 27/03 |
| 3 | Sync & Conectividade | 8 | 32 pts | Semana 3-4 | 03/04 |
| 4 | Backend Cloud - Core | 9 | 38 pts | Semana 3-4 | 10/04 |
| 5 | Backend Cloud - Features | 6 | 24 pts | Semana 4-5 | 17/04 |
| 6 | UI Desktop - Fundação | 10 | 40 pts | Semana 4-5 | 17/04 |
| 7 | UI Desktop - Dashboard | 12 | 49 pts | Semana 5-6 | 24/04 |
| 8 | Observabilidade & DevOps | 7 | 29 pts | Semana 6-7 | 01/05 |
| 9 | Piloto & Validação | 1 | 5 pts | Semana 8 | 08/05 |

---

## Regras de Execução

1. **Dependências estritas:** Nunca inicie uma task se suas dependências não estão concluídas
2. **Paralelismo:** Tasks no mesmo nível podem ser executadas em paralelo
3. **Sequência:** Tasks em níveis diferentes devem seguir ordem sequencial
4. **Checkpoint:** Cada fase deve ter validação antes de avançar

---

## FASE 1 — Fundação & Agent Core

**Objetivo:** Criar a estrutura base do monorepo e o domínio core do sistema de tracking.

**Duração:** Semana 1-2 | **Target:** 20/03/2026 | **Pontos:** 44 pts

### Nível 1 — Setup Inicial (sem dependências)

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-80 | Setup: Estrutura do Monorepo e Solution .NET | 3 | - |

### Nível 2 — Domain Layer

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-81 | Domain: Entidades e Value Objects do Tracking | 5 | CX-80 |
| CX-82 | Domain: Agregado TrackingState e regras de Pause/Resume | 4 | CX-80 |

### Nível 3 — Application Layer

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-86 | Application: Use Case ConsolidateSession (Loop de Persistência) | 5 | CX-81, CX-82 |
| CX-88 | Application: Use Cases Pause/Resume Tracking | 4 | CX-81, CX-82 |
| CX-89 | Application: Use Case GetLocalDashboard (Relatório Local) | 4 | CX-81, CX-82 |
| CX-90 | Application: Use Case RecordActiveWindow (Catálogo de Apps) | 3 | CX-81, CX-82 |

### Nível 4 — Windows Service Base

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-83 | Infra: Windows Service (AgentService) – Scaffolding | 4 | CX-86 |

### Nível 5 — SQLite Infrastructure

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-87 | Infra: SQLite – Setup, Migrations e Repositórios | 6 | CX-83 |

### Entregáveis da Fase 1
- [ ] Monorepo estruturado com Solution .NET
- [ ] Domain layer com entidades e value objects
- [ ] Use cases de tracking funcionando
- [ ] Windows Service base operacional
- [ ] SQLite configurado com migrations

---

## FASE 2 — Infraestrutura do Agent

**Objetivo:** Implementar adapters de captura, notificações e testes do agent.

**Duração:** Semana 2-3 | **Target:** 27/03/2026 | **Pontos:** 35 pts

### Nível 1 — Adapters de Captura

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-84 | Infra: Adapter IActiveWindowProvider (WinEvent + Fallback Polling) | 6 | CX-83 |
| CX-85 | Infra: Adapter IIdleDetector (GetLastInputInfo via P/Invoke) | 3 | CX-83 |

### Nível 2 — Sistema de Notificações

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-136 | Infra: Sistema de Notificações Windows (Toast) | 3 | CX-83 |

### Nível 3 — UX Features

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-142 | Agent: Auto-Resume por Detecção de Atividade (Toast de Confirmação) | 4 | CX-85, CX-88, CX-136 |

### Nível 4 — Focus Mode Engine

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-138 | Agent: FocusModeEngine (Pomodoro + Ciclo Ultradian + Toast) | 6 | CX-85, CX-88, CX-136 |

### Nível 5 — QA e Validação

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-91 | QA: Benchmark e Soak Test do Agent (7 dias) | 5 | Toda Fase 1 + Fase 2 |

### Entregáveis da Fase 2
- [ ] Captura de janela ativa funcionando
- [ ] Detecção de idle funcionando
- [ ] Sistema de notificações Toast
- [ ] Auto-resume por detecção de atividade
- [ ] Focus Mode Engine (Pomodoro/Ultradian)
- [ ] Agent testado e validado

---

## FASE 3 — Sync & Conectividade

**Objetivo:** Implementar sincronização do Agent com a Cloud via Outbox Pattern.

**Duração:** Semana 3-4 | **Target:** 03/04/2026 | **Pontos:** 32 pts

### Nível 1 — Outbox Pattern

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-92 | Domain/Infra: Outbox Pattern no Agent (SQLite) | 5 | CX-87 |

### Nível 2 — Sync Worker

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-93 | Application: SyncWorker (Loop Batch + Retry/Backoff) | 5 | CX-92 |
| CX-94 | Infra: HTTP Sync Client (ISyncTransport) | 4 | CX-92 |

### Nível 3 — Autenticação e Device

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-96 | Backend: ActivateDevice e Heartbeat | 3 | CX-93, CX-94 |
| CX-97 | Infra: Token JWT no Agent (DPAPI + Refresh Automático) | 3 | CX-93, CX-94 |

### Nível 4 — Diagnóstico

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-98 | Infra: Registro de Erros de Sync e Diagnóstico | 2 | CX-93, CX-94 |

### Nível 5 — Validação E2E

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-99 | Testes E2E: Agent → Cloud → Postgres | 5 | Fase 3 + CX-95 (Fase 4) |

### Entregáveis da Fase 3
- [ ] Outbox Pattern implementado
- [ ] Sync Worker com retry/backoff
- [ ] HTTP Client para sync
- [ ] Autenticação JWT com DPAPI
- [ ] Sistema de diagnóstico
- [ ] Testes E2E passando

---

## FASE 4 — Backend Cloud - Core

**Objetivo:** Implementar o backend ASP.NET Core com auth, multi-tenancy e ingestão.

**Duração:** Semana 3-4 | **Target:** 10/04/2026 | **Pontos:** 38 pts

> **Nota:** Pode iniciar em paralelo com Fase 3 após CX-87

### Nível 1 — Setup Backend

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-109 | Setup: ASP.NET Core + EF Core + Neon Postgres | 4 | - |

### Nível 2 — Autenticação

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-110 | Backend: Auth – Login, Refresh Token, JWT e Ativação de Device | 5 | CX-109 |

### Nível 3 — Multi-tenancy

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-111 | Backend: RBAC e Multi-tenancy (org_id em todas as queries) | 4 | CX-110 |

### Nível 4 — Ingestão e Organizations

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-95 | Backend: Endpoint de Ingestão Idempotente | 5 | CX-111 |
| CX-114 | Backend: Organizations, Users, Convites e Criação de Conta (B2B/B2C) | 4 | CX-111 |

### Nível 5 — Políticas e Proteção

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-113 | Backend: Políticas (Horário de Trabalho + Exclusões de Apps) | 5 | CX-111 |
| CX-115 | Backend: Audit Log Mínimo (MVP) | 3 | CX-111 |
| CX-116 | Backend: Rate Limiting e Proteção Básica | 3 | CX-111 |

### Nível 6 — Jobs e Workers

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-117 | Backend: Workers/Jobs – Retenção, Agregação e Limpeza (Hangfire) | 4 | CX-111 |

### Entregáveis da Fase 4
- [ ] Backend ASP.NET Core configurado
- [ ] Sistema de autenticação JWT
- [ ] Multi-tenancy funcionando
- [ ] Endpoint de ingestão idempotente
- [ ] CRUD de Organizations/Users
- [ ] Políticas de trabalho
- [ ] Audit Log básico
- [ ] Rate Limiting

---

## FASE 5 — Backend Cloud - Features

**Objetivo:** Implementar features avançadas do backend (relatórios, focus score, teams).

**Duração:** Semana 4-5 | **Target:** 17/04/2026 | **Pontos:** 24 pts

### Nível 1 — Relatórios

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-112 | Backend: Relatórios – Daily Summary e Top Apps | 5 | CX-111, CX-113 |

### Nível 2 — Focus Score

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-133 | Backend: focus_sessions + Cálculo do Focus Score | 4 | CX-109, CX-95, CX-113 |

### Nível 3 — Focus Mode Policy

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-137 | Backend: Política de Modo de Foco (Pomodoro / Ciclo Ultradian) | 3 | CX-109, CX-95, CX-113 |

### Nível 4 — Teams

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-135 | Backend + Frontend: Times (Teams) e Granularidade de Acesso do Gestor | 5 | CX-111, CX-113, CX-114 |

### Nível 5 — Categorização

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-143 | Backend: Sistema de Categorização de Apps/Sites (Lista Global + Override) | 5 | CX-111, CX-113 |

### Nível 6 — Export

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-108 | Backend: Export CSV (Colaborador e Gestor) | 2 | CX-111, CX-112 |

### Entregáveis da Fase 5
- [ ] Relatórios de Daily Summary
- [ ] Focus Score calculado
- [ ] Política de Focus Mode
- [ ] Times e granularidade de acesso
- [ ] Sistema de categorização de apps
- [ ] Export CSV

---

## FASE 6 — UI Desktop - Fundação

**Objetivo:** Criar a estrutura base da UI Desktop com React + WebView2.

**Duração:** Semana 4-5 | **Target:** 17/04/2026 | **Pontos:** 40 pts

> **Nota:** Pode iniciar após CX-83 (Fase 1)

### Nível 1 — Setup Desktop

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-100 | Setup: DesktopHost .NET + WebView2 + IPC Server | 5 | CX-83 |
| CX-101 | Setup: Projeto React (Vite + TypeScript + Zustand) | 3 | CX-83 |
| CX-140 | Agent + DesktopHost: Modo de Exibição (Background/Foreground) | 5 | CX-100 |
| CX-141 | DesktopHost: Modo de Exibição por DisplayMode (Background vs Foreground) | 4 | CX-100 |

### Nível 2 — Design System

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-132 | Design System: Tokens, Paleta Dark Theme e Componentes Primitivos | 3 | CX-101 |

### Nível 3 — IPC e Auth

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-105 | Frontend: Integração IPC Completa (Commands + Events + Auth) | 4 | CX-100, CX-101, CX-132, CX-110 |
| CX-106 | Frontend: Auth Flow – Login, DPAPI e Troca de Senha Obrigatória | 3 | CX-100, CX-101, CX-132, CX-110 |

### Nível 4 — Telas Base

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-103 | Frontend: Tela de Status e Controle do Agent | 3 | CX-105, CX-106 |
| CX-104 | Frontend: Tela de Configurações (Horário + Exclusões) | 4 | CX-105, CX-106 |
| CX-145 | Frontend: Tela de Troca de Senha Obrigatória (Primeiro Acesso) | 4 | CX-106 |

### Entregáveis da Fase 6
- [ ] DesktopHost com WebView2
- [ ] Projeto React configurado
- [ ] Design System Dark Theme
- [ ] IPC funcionando
- [ ] Auth Flow implementado
- [ ] Telas de Status e Configurações

---

## FASE 7 — UI Desktop - Dashboard

**Objetivo:** Implementar o Dashboard completo e painéis administrativos.

**Duração:** Semana 5-6 | **Target:** 24/04/2026 | **Pontos:** 49 pts

### Nível 1 — Dashboard Base

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-102 | Frontend: Dashboard do Colaborador (Top Apps + Status) | 5 | CX-105, CX-106, CX-89 |

### Nível 2 — Componentes do Dashboard

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-127 | Dashboard: Sidebar de Navegação | 3 | CX-102, CX-132 |
| CX-128 | Dashboard: Cards de Resumo Superior (Tempo + Foco + Timer) | 5 | CX-102, CX-132 |
| CX-129 | Dashboard: Timeline de Atividade (Heatmap Horizontal) | 4 | CX-102, CX-132 |
| CX-130 | Dashboard: Cards de Categorias, Apps & Sites e Projetos | 4 | CX-102, CX-132 |
| CX-131 | Dashboard: Painel Direito (Equipe Agora + Tempo por Projeto) | 5 | CX-102, CX-132 |

### Nível 3 — Focus Mode UI

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-139 | Frontend: Card Timer com Modo de Foco (Pomodoro / Ciclo Ultradian) | 5 | CX-105, CX-128, CX-137 |

### Nível 4 — Painel Admin

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-134 | Frontend: Painel de Administração (Membros + Políticas) | 5 | CX-111, CX-113, CX-114, CX-132, CX-135 |
| CX-144 | Frontend Admin: Aba de Categorização de Apps/Sites (Override da Org) | 4 | CX-134, CX-143 |

### Nível 5 — Dashboard Gestor e Web

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-107 | Frontend: Dashboard do Gestor (Web – Relatórios por Equipe) | 5 | CX-111, CX-113, CX-114, CX-132, CX-135 |
| CX-146 | Frontend Web: Tela de Registro – Criar Organização (B2B e B2C) | 4 | CX-114 |

### Entregáveis da Fase 7
- [ ] Dashboard do Colaborador completo
- [ ] Sidebar de navegação
- [ ] Cards de resumo
- [ ] Timeline de atividade
- [ ] Painel de administração
- [ ] Dashboard do Gestor
- [ ] Tela de registro

---

## FASE 8 — Observabilidade & DevOps

**Objetivo:** Implementar observabilidade, segurança e CI/CD.

**Duração:** Semana 6-7 | **Target:** 01/05/2026 | **Pontos:** 29 pts

> **Nota:** Só iniciar após Fases 1-7 concluídas

### Nível 1 — Observabilidade

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-120 | Observabilidade: Logs Estruturados com Correlação (Agent + Backend) | 4 | Fases 1-7 |
| CX-121 | Observabilidade: Métricas do Agent (CPU, RAM, Sync Backlog) | 3 | Fases 1-7 |

### Nível 2 — Hardening

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-122 | Hardening: Revisão de Segurança e Privacidade | 5 | Fases 1-7 |

### Nível 3 — DevOps

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-118 | DevOps: Docker + CI/CD para o Backend | 4 | Fase 4, Fase 5 |
| CX-123 | DevOps: Instalador MSIX do Agent para Windows | 5 | Fase 1, Fase 2, Fase 6 |

### Nível 4 — QA e Performance

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-119 | QA: Testes de Integração com Testcontainers (Suite Completa) | 4 | Fase 4, Fase 5 |
| CX-126 | QA/Performance: Suite Completa (BenchmarkDotNet + k6) | 4 | Fases 1-7 |

### Nível 5 — Documentação

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-125 | Documentação: Runbooks e README Final | 3 | Fases 1-7 |

### Entregáveis da Fase 8
- [ ] Logs estruturados com correlação
- [ ] Métricas do Agent
- [ ] Revisão de segurança
- [ ] Docker + CI/CD
- [ ] Instalador MSIX
- [ ] Testes de integração
- [ ] Suite de performance
- [ ] Documentação final

---

## FASE 9 — Piloto & Validação

**Objetivo:** Validar o sistema em ambiente real com usuários.

**Duração:** Semana 8 | **Target:** 08/05/2026 | **Pontos:** 5 pts

> **Nota:** Gate final — só iniciar após Fases 1-8 concluídas

### Nível 1 — Piloto

| ID | Task | Pontos | Dependências |
|----|------|--------|--------------|
| CX-124 | Piloto: Setup de 7 Máquinas e Monitoramento 7 Dias | 5 | Fases 1-8 |

### Critérios de Aceite do Piloto
- [ ] 7 máquinas rodando por 7 dias consecutivos
- [ ] Sync funcionando sem erros
- [ ] Performance dentro dos parâmetros
- [ ] Feedback dos usuários coletado
- [ ] Bugs críticos resolvidos

---

## Cronograma Resumido

```
Semana 1 (09-15/03):  FASE 1 — Fundação & Agent Core
Semana 2 (16-22/03):  FASE 1 (cont.) + FASE 2 — Infraestrutura do Agent
Semana 3 (23-29/03):  FASE 2 (cont.) + FASE 3 — Sync + FASE 4 — Backend Core
Semana 4 (30-05/04):  FASE 3 (cont.) + FASE 4 (cont.) + FASE 5 — Backend Features
Semana 5 (06-12/04):  FASE 5 (cont.) + FASE 6 — UI Desktop Fundação
Semana 6 (13-19/04):  FASE 6 (cont.) + FASE 7 — UI Desktop Dashboard
Semana 7 (20-26/04):  FASE 7 (cont.) + FASE 8 — Observabilidade & DevOps
Semana 8 (27-03/05):  FASE 8 (cont.) + FASE 9 — Piloto
Semana 9 (04-08/05):  FASE 9 — Piloto (validação final)
```

---

## Dependências entre Fases

```
FASE 1 ──┬──> FASE 2 ──> FASE 3 ──┐
         │                        │
         └──> FASE 6 ──> FASE 7 ──┤
                                  │
         FASE 4 ──> FASE 5 ───────┼──> FASE 8 ──> FASE 9
                                  │
              (paralelo)          │
```

**Fases Paralelas:**
- Fase 3, 4 e 6 podem rodar em paralelo após Fase 1
- Fase 5 e 7 podem rodar em paralelo após suas dependências

---

*Documento gerado em 09/03/2026 — Time Track MVP (Fase 1)*
