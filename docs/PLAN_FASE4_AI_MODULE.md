# Plano de Implementação — Módulo z.ai (Fase 4)

> **Projeto:** TimeTrack (ChronosX)
> **Data:** 2026-04-25
> **Issues Linear:** CX-208 a CX-222
> **Branch:** feat-phase2

---

## Contexto

Integração com z.ai para adicionar inteligência ao TimeTrack: classificação de apps via IA, detecção de padrões comportamentais, narrativas semanais, alertas inteligentes e pipeline de feedback. Tudo com fallback determinístico — o sistema nunca falha silenciosamente se o z.ai estiver indisponível.

### Stack Atual Relevante

| Componente | Tecnologia |
|---|---|
| Backend | .NET 8, EF Core, PostgreSQL, MediatR, Hangfire |
| Frontend | React 18, Zustand, Tailwind CSS v4, Radix UI, Recharts |
| Cache | IMemoryCache (sem Redis no momento) |
| Classificação de Apps | `AppProductivityClassifier` + `AppCategoryGlobal` + `AppCategoryOverride` |
| Focus Score | `FocusScoreCalculator` com penalties/bonuses (0-100) |
| Agregação Diária | `DailySummary` + `DailyFocusScore` (jobs Hangfire) |

### Desvios Recomendados vs. Issues do Linear

| Item Original | Recomendação | Motivo |
|---|---|---|
| Criar tabela `feature_daily` separada | Estender `DailyFocusScore` com campos adicionais | Já existe agregação diária + focus score. Evita duplicação |
| Redis para cache de classificações | `IMemoryCache` para MVP | Redis exige infra adicional. IMemoryCache resolve para o volume atual |
| z.ai como única fonte de classificação | 2 camadas: z.ai sugere, regras locais como fallback | `AppProductivityClassifier` determinístico já funciona — z.ai complementa |

---

## FASE 1 — Fundação (Infraestrutura + Feature Store)

**Objetivo:** Preparar o terreno de dados e o cliente z.ai. Sem UI, sem features visíveis ao usuário.

**Issues:** CX-208, CX-209, CX-210, CX-211

---

### 1.1 — Estender DailyFocusScore com Features de Agregação (CX-208)

**O que fazer:** Adicionar campos de `feature_daily` ao `DailyFocusScore` existente e atualizar o `FocusScoreJob`.

**Campos novos na entidade `DailyFocusScore`:**

```sql
ALTER TABLE daily_focus_scores ADD COLUMN productive_seconds INT DEFAULT 0;
ALTER TABLE daily_focus_scores ADD COLUMN distraction_seconds INT DEFAULT 0;
ALTER TABLE daily_focus_scores ADD COLUMN neutral_seconds INT DEFAULT 0;
ALTER TABLE daily_focus_scores ADD COLUMN context_switches_count INT DEFAULT 0;
ALTER TABLE daily_focus_scores ADD COLUMN interruption_count INT DEFAULT 0;
ALTER TABLE daily_focus_scores ADD COLUMN top_app_exe TEXT;
ALTER TABLE daily_focus_scores ADD COLUMN top_app_seconds INT DEFAULT 0;
ALTER TABLE daily_focus_scores ADD COLUMN distinct_apps_count INT DEFAULT 0;
ALTER TABLE daily_focus_scores ADD COLUMN browser_seconds INT DEFAULT 0;
ALTER TABLE daily_focus_scores ADD COLUMN productivity_ratio FLOAT DEFAULT 0;
ALTER TABLE daily_focus_scores ADD COLUMN focus_sessions_count INT DEFAULT 0;
ALTER TABLE daily_focus_scores ADD COLUMN focus_sessions_completed INT DEFAULT 0;
ALTER TABLE daily_focus_scores ADD COLUMN longest_focus_seconds INT DEFAULT 0;
ALTER TABLE daily_focus_scores ADD COLUMN avg_focus_seconds INT DEFAULT 0;
```

**Alterações no `FocusScoreJob`:**
- Após calcular o focus score existente, popular os novos campos
- `productive_seconds` / `distraction_seconds` / `neutral_seconds`: agregação de `ActivitySession` por categoria
- `context_switches_count`: contar transições app→app acima de threshold (ex: < 30s)
- `interruption_count`: transições produtivo→distração
- `top_app_exe` + `top_app_seconds`: app com mais tempo no dia
- `distinct_apps_count`: COUNT(DISTINCT app_identifier)
- `browser_seconds`: soma de sessions onde app_identifier = browser
- `productivity_ratio`: productive_seconds / (productive + distraction + neutral)
- `focus_sessions_count` / `focus_sessions_completed`: contar `FocusSession` do dia
- `longest_focus_seconds`: MAX(duration) de FocusSession
- `avg_focus_seconds`: AVG(duration) de FocusSession

**Reprocessamento manual:**
- Endpoint `POST /api/v1/admin/recompute-features?date=YYYY-MM-DD`
- Reexecuta o `FocusScoreJob` para a data especificada

**Critérios de Aceite:**
- [ ] FocusScoreJob popula todos os campos novos
- [ ] `productive_seconds + distraction_seconds + neutral_seconds ≈ total_active_seconds`
- [ ] `productivity_ratio = productive / total_active` (com proteção div/0)
- [ ] Reprocessamento manual funciona para qualquer data
- [ ] Dias sem tracking = DailyFocusScore com zeros (não ausência)

---

### 1.2 — Feature Store Semanal e Mensal (CX-209)

**O que fazer:** Criar entidades e jobs para agregação semanal e mensal de features.

**Nova entidade `FeatureWeekly`:**

```sql
CREATE TABLE feature_weekly (
  id                    UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id               UUID NOT NULL,
  org_id                UUID NOT NULL,
  week_start            DATE NOT NULL,  -- sempre segunda-feira
  avg_focus_score       FLOAT,
  avg_productive_ratio  FLOAT,
  total_active_hours    FLOAT,
  avg_context_switches  FLOAT,
  avg_interruption_count FLOAT,
  trend_focus_score     FLOAT,   -- delta vs semana anterior
  trend_productive_ratio FLOAT,  -- delta vs semana anterior
  computed_at           TIMESTAMPTZ NOT NULL DEFAULT now(),
  UNIQUE (user_id, week_start)
);
```

**Nova entidade `FeatureMonthly`:**

```sql
CREATE TABLE feature_monthly (
  id                    UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id               UUID NOT NULL,
  org_id                UUID NOT NULL,
  month_start           DATE NOT NULL,  -- sempre dia 1
  avg_focus_score       FLOAT,
  avg_productive_ratio  FLOAT,
  total_active_hours    FLOAT,
  trend_focus_score     FLOAT,  -- delta vs mês anterior
  computed_at           TIMESTAMPTZ NOT NULL DEFAULT now(),
  UNIQUE (user_id, month_start)
);
```

**Novos Jobs:**

| Job | Schedule | Lógica |
|---|---|---|
| `WeeklyFeatureAggregationJob` | Toda segunda 03:00 UTC | Agrega `DailyFocusScore` dos últimos 7 dias em `FeatureWeekly`. Calcula `trend_*` comparando com semana anterior |
| `MonthlyFeatureAggregationJob` | Dia 1 de cada mês 03:00 UTC | Agrega `DailyFocusScore` dos últimos 30 dias em `FeatureMonthly`. Calcula `trend_*` comparando com mês anterior |

**Reprocessamento manual:**
- `POST /api/v1/admin/recompute-features/weekly?weekStart=YYYY-MM-DD`
- `POST /api/v1/admin/recompute-features/monthly?monthStart=YYYY-MM-DD`

**Critérios de Aceite:**
- [ ] `feature_weekly` populado corretamente toda segunda
- [ ] `feature_monthly` populado corretamente todo mês
- [ ] Campos `trend_*` calculados como delta relativo ao período anterior
- [ ] Reprocessamento manual disponível para ambas as tabelas
- [ ] Semana sem dados = zeros (não ausência)

---

### 1.3 — AI Decision Log (CX-210)

**O que fazer:** Criar tabela de auditoria para toda interação com IA.

**Nova entidade `AiDecisionLog`:**

```sql
CREATE TABLE ai_decision_log (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id         UUID,
  org_id          UUID NOT NULL,
  decision_type   TEXT NOT NULL,
  -- valores: 'app_classification' | 'pattern_detected'
  --          | 'weekly_narrative' | 'alert_generated'
  input_data      JSONB NOT NULL,
  output          JSONB NOT NULL,
  model_version   TEXT NOT NULL,  -- 'rules-v1' | 'zai-v1' | 'zai-v1.1'
  confidence      FLOAT,          -- 0.0-1.0
  tokens_used     INT,
  latency_ms      INT,
  was_reviewed    BOOLEAN NOT NULL DEFAULT false,
  review_outcome  TEXT,
  -- valores: 'accepted' | 'rejected' | 'corrected'
  correct_value   JSONB,
  created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_ai_decision_log_user_type
  ON ai_decision_log (user_id, decision_type, created_at DESC);
CREATE INDEX idx_ai_decision_log_org_type
  ON ai_decision_log (org_id, decision_type, created_at DESC);
CREATE INDEX idx_ai_decision_log_unreviewed
  ON ai_decision_log (was_reviewed, decision_type) WHERE was_reviewed = false;
```

**Critérios de Aceite:**
- [ ] Toda chamada ao z.ai gera entrada no log
- [ ] Toda classificação por regras (fallback) logada com `model_version = 'rules-v1'`
- [ ] `tokens_used` e `latency_ms` preenchidos em chamadas z.ai
- [ ] Endpoint `GET /api/v1/internal/ai-decisions` (Admin only, paginado)

---

### 1.4 — Cliente z.ai com Resiliência (CX-211)

**O que fazer:** Criar camada de integração com z.ai — o coração do módulo de IA.

**Estrutura de projeto:**

```
src/backend/TimeTrack.Backend.AI/
├── Interfaces/
│   └── IAIService.cs
├── Models/
│   ├── ClassificationResult.cs
│   ├── AppClassificationRequest.cs
│   ├── WeeklyFeatureContext.cs
│   ├── UserPattern.cs
│   ├── AlertContext.cs
│   └── ZAiResponse.cs
├── Services/
│   ├── ZAiService.cs           -- implementação principal
│   ├── ZAiHttpClient.cs        -- HTTP client com resiliência
│   └── FallbackRuleService.cs  -- regras determinísticas
├── Configuration/
│   └── ZAiOptions.cs
└── Extensions/
    └── ServiceCollectionExtensions.cs
```

**Interface `IAIService`:**

```csharp
public interface IAIService
{
    Task<ClassificationResult> ClassifyAppAsync(
        AppClassificationRequest request,
        CancellationToken ct = default);

    Task<string> GenerateWeeklyNarrativeAsync(
        WeeklyFeatureContext context,
        CancellationToken ct = default);

    Task<string> DescribePatternAsync(
        UserPatternContext pattern,
        CancellationToken ct = default);

    Task<string> GenerateAlertMessageAsync(
        AlertContext context,
        CancellationToken ct = default);
}
```

**Configuração `ZAiOptions`:**

```csharp
public class ZAiOptions
{
    public string ApiKey { get; set; } = string.Empty;     // env var: ZAI_API_KEY
    public string BaseUrl { get; set; } = string.Empty;    // env var: ZAI_BASE_URL
    public string ModelVersion { get; set; } = "zai-v1";
    public int TimeoutSeconds { get; set; } = 10;
    public int MaxRetries { get; set; } = 3;
    public int CircuitBreakerFailures { get; set; } = 5;
    public int CircuitBreakerDurationSeconds { get; set; } = 60;
    public int CacheDurationHours { get; set; } = 24;
}
```

**Resiliência com Polly:**

```
Retry: 3 tentativas, backoff exponencial (1s, 2s, 4s)
Timeout: 10s por chamada
Circuit Breaker: 5 falhas consecutivas → abre por 60s → fallback
Cache: IMemoryCache, 24h para classificações de app
Fallback: FallbackRuleService delega ao AppProductivityClassifier existente
```

**Logging:**
- Toda chamada (sucesso ou fallback) registrada no `AiDecisionLog`
- `tokens_used` e `latency_ms` extraídos da resposta do z.ai

**Registro no DI (Program.cs):**

```csharp
builder.Services.Configure<ZAiOptions>(builder.Configuration.GetSection("ZAi"));
builder.Services.AddHttpClient<IZAiHttpClient, ZAiHttpClient>();
builder.Services.AddSingleton<IAIService, ZAiService>();
```

**Critérios de Aceite:**
- [ ] `IAIService` registrado no DI
- [ ] Retry + circuit breaker testados com mock de falha
- [ ] Cache de classificações evita chamadas duplicadas ao z.ai
- [ ] Fallback para regras quando z.ai indisponível
- [ ] Toda chamada logada em `AiDecisionLog` com `tokens_used` e `latency_ms`
- [ ] API key via env var, nunca hardcoded
- [ ] Testes unitários com mock HTTP para todos os caminhos (sucesso, retry, fallback)

**Entregáveis da Fase 1:**
- 3 migrations EF Core
- 1 novo projeto `TimeTrack.Backend.AI`
- `IAIService` funcional com resiliência
- 3 Hangfire jobs novos + `FocusScoreJob` atualizado
- **Zero mudanças no frontend**

---

## FASE 2 — Classificação de Apps via IA

**Objetivo:** z.ai classifica apps unknown/neutral, Admin revisa sugestões via interface dedicada.

**Issues:** CX-212, CX-213

---

### 2.1 — Job de Classificação + API (CX-212)

**Job `AppClassificationJob`:**
- Schedule: semanal (domingo 22:00 UTC, antes do WeeklyNarrativeJob)
- Coleta apps marcados como `neutral` ou `unknown` na `app_category_global`
- Para cada app sem classificação aprovada nos últimos 30 dias:
  - Monta contexto a partir de `DailyFocusScore` dos últimos 30 dias
  - Envia ao `IAIService.ClassifyAppAsync()`
  - Salva resultado em `AiDecisionLog` com `decision_type = 'app_classification'`

**Contexto enviado ao z.ai:**

```json
{
  "exe_name": "notion.exe",
  "sample_window_titles": ["Meu projeto - Notion", "Tarefas da semana - Notion"],
  "avg_daily_seconds": 5400,
  "usage_days_last_30": 22,
  "usage_hours": { "morning": 0.4, "afternoon": 0.5, "evening": 0.1 },
  "correlation_with_focus_score": 0.72,
  "co_occurring_apps": ["code.exe", "slack.exe", "figma.exe"]
}
```

**Resposta esperada:**

```json
{
  "category": "productive",
  "subcategory": "productivity_tools",
  "confidence": 0.94,
  "reasoning": "App usado regularmente em horário comercial..."
}
```

**Fallback (z.ai indisponível):**
1. Título contém keywords produtivas → `productive`
2. Correlação positiva com `focus_score` → `productive`
3. Correlação negativa com `focus_score` → `distraction`
4. Padrão irregular de uso → `distraction`
5. Sem dados suficientes → `neutral` (confiança: 0.3)

**API Endpoints:**

```
GET  /api/v1/admin/ai/classifications
     Query: ?status=pending&confidence=low|high|all&page=1&pageSize=20
     Response: lista de AiDecisionLog com decision_type='app_classification'

POST /api/v1/admin/ai/classifications/{decisionId}/review
     Body: { "outcome": "accepted"|"rejected"|"corrected",
             "correctCategory": "productive",        // se corrected
             "correctSubcategory": "development" }    // se corrected
```

**Comportamento pós-revisão:**
- `accepted`: cria `AppCategoryOverride` + `review_outcome = accepted`
- `corrected`: cria `AppCategoryOverride` com valor correto + `review_outcome = corrected` + salva `correct_value`
- `rejected`: `review_outcome = rejected`, app não recebe nova sugestão por 30 dias
- Confidence < 0.6: badge "baixa confiança", revisão obrigatória

**Critérios de Aceite:**
- [ ] Job classifica todos os apps neutral/unknown com contexto de DailyFocusScore
- [ ] Sugestões ficam pendentes até revisão (nunca auto-aplicadas)
- [ ] Fallback para regras quando z.ai indisponível
- [ ] Confidence < 0.6 marcados como baixa confiança
- [ ] Decisão logada em AiDecisionLog com input_data e output completos

---

### 2.2 — Frontend: Interface de Revisão de Classificações (CX-213)

**Localização:** Settings → nova seção "Sugestões de Classificação" (Admin only)

**Layout:**

```
┌─────────────────────────────────────────────────────────────┐
│  Sugestões de Classificação (12 pendentes)  [Aceitar todas] │
│  Filtro: [Todas] [Alta confiança] [Baixa confiança]         │
├─────────────────────────────────────────────────────────────┤
│  📦 notion.exe                                              │
│  Sugestão: Produtivo / Ferramentas de Produtividade    94%  │
│  "Usado regularmente em horário comercial com alta          │
│   correlação com foco, títulos sugerem gestão de projetos"  │
│  [✅ Aceitar]  [✏️ Corrigir]  [❌ Rejeitar]                 │
├─────────────────────────────────────────────────────────────┤
│  📦 meu-app-interno.exe                              ⚠️ 58% │
│  Sugestão: Produtivo / Desenvolvimento                      │
│  "Baixa confiança — padrão de uso irregular"                │
│  [✅ Aceitar]  [✏️ Corrigir]  [❌ Rejeitar]                 │
└─────────────────────────────────────────────────────────────┘
```

**Comportamentos:**
- Filtro por nível de confiança (Todos / Alta ≥ 0.6 / Baixa < 0.6)
- "Aceitar todas" aceita apenas confidence ≥ 0.90
- Após ação, card anima para fora e badge atualiza
- Dropdown de categorias no "Corrigir" usa lista de `AppCategoryGlobal`

**Critérios de Aceite:**
- [ ] Lista de sugestões pendentes exibida com confiança e motivo
- [ ] Aceitar cria `AppCategoryOverride` e remove da lista
- [ ] Corrigir salva categoria escolhida pelo Admin
- [ ] Rejeitar registra feedback negativo sem criar override
- [ ] Badge de contagem de pendentes atualiza após cada ação
- [ ] Filtro por nível de confiança funciona

**Entregáveis da Fase 2:**
- 1 Hangfire job novo
- 2+ endpoints de API
- 1 seção nova no Admin Panel (frontend)
- Classificação de apps flutuando entre regras e z.ai

---

## FASE 3 — Detecção de Padrões e Anomalias

**Objetivo:** Detectar padrões comportamentais e anomalias nos dados de features agregadas.

**Issues:** CX-214, CX-215

---

### 3.1 — Detecção de Padrões de Foco e Dispersão (CX-214)

**Nova entidade `UserPattern`:**

```sql
CREATE TABLE user_patterns (
  id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id      UUID NOT NULL,
  org_id       UUID NOT NULL,
  pattern_tag  TEXT NOT NULL,
  detected_at  DATE NOT NULL,
  strength     FLOAT NOT NULL,  -- 0.0-1.0
  evidence     JSONB NOT NULL,
  description  TEXT,            -- narrativa gerada pelo z.ai
  is_active    BOOLEAN NOT NULL DEFAULT true,
  created_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
  UNIQUE (user_id, pattern_tag, detected_at)
);

CREATE INDEX idx_user_patterns_user_active
  ON user_patterns (user_id, is_active, detected_at DESC);
```

**Job `PatternDetectionJob`:**
- Schedule: diário, após `FocusScoreJob` (ex: 01:00 UTC)
- Analisa últimos 7 dias de `DailyFocusScore`

**5 padrões determinísticos:**

| Tag | Condição | Strength |
|---|---|---|
| `morning_productive` | productive_ratio > 0.7 entre 08-12h em ≥ 4/5 dias úteis | dias_confirmados / 5 |
| `afternoon_focus_drop` | focus_score 14-17h < 50% do focus_score 08-12h em ≥ 4/7 dias | dias_confirmados / 7 |
| `high_context_switching` | context_switches_count > média_org × 2 em ≥ 3 dias | dias_confirmados / 7 |
| `deep_work_capable` | longest_focus_seconds > 5400 (90min) em ≥ 2 dias | dias_confirmados / 7 |
| `high_distraction_risk` | distraction_seconds > total_active × 0.3 em ≥ 3 dias | dias_confirmados / 7 |

**Descrição via z.ai:**
- Após detectar padrão determinísticamente, z.ai gera `description` em PT-BR
- Ex: "Você tem sido consistentemente mais produtivo pela manhã. Seus melhores dias começam com sessões de foco entre 9h e 11h."
- Chamada: `IAIService.DescribePatternAsync()`

**Manutenção:**
- Padrões > 30 dias sem re-confirmação → `is_active = false`
- Padrão não detectado = sem linha (não registra ausência)

**Critérios de Aceite:**
- [ ] Job detecta todos os 5 padrões corretamente com dados sintéticos
- [ ] `strength` calculado proporcionalmente (ex: 4/5 dias = 0.8)
- [ ] `description` em PT-BR gerado pelo z.ai para cada padrão
- [ ] Padrões antigos (> 30 dias) marcados como inativos
- [ ] Padrão não detectado = sem linha na tabela

---

### 3.2 — Detecção de Anomalias Comportamentais (CX-215)

**Nova entidade `BehavioralAnomaly`:**

```sql
CREATE TABLE behavioral_anomalies (
  id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id        UUID NOT NULL,
  org_id         UUID NOT NULL,
  anomaly_type   TEXT NOT NULL,
  severity       TEXT NOT NULL,  -- 'info' | 'warning' | 'alert'
  detected_at    DATE NOT NULL,
  evidence       JSONB NOT NULL,
  baseline_value FLOAT,
  actual_value   FLOAT,
  created_at     TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_behavioral_anomalies_user_date
  ON behavioral_anomalies (user_id, detected_at DESC);
CREATE INDEX idx_behavioral_anomalies_org_severity
  ON behavioral_anomalies (org_id, severity, detected_at DESC);
```

**Job `AnomalyDetectionJob`:**
- Schedule: diário, após `PatternDetectionJob` (ex: 01:30 UTC)
- Baseline: últimos 30 dias de `DailyFocusScore` por usuário
- Sem baseline suficiente (< 14 dias de dados) → pula usuário

**4 anomalias com thresholds adaptativos:**

| Tipo | Condição | Severidade |
|---|---|---|
| `productivity_drop` | productive_ratio < (média_30d - 2×stddev) | warning |
| `absent_workday` | total_tracked_seconds < 3600 em dia útil | info |
| `exceptionally_long_day` | total_active_seconds > média_diária × 1.8 | info |
| `focus_collapse` | focus_score < (média_30d × 0.5) por ≥ 3 dias seguidos | alert |

**Critérios de Aceite:**
- [ ] Thresholds calculados sobre últimos 30 dias de cada usuário
- [ ] Sem anomalias nos primeiros 14 dias (sem baseline)
- [ ] Severidade correta para cada tipo
- [ ] Anomalias alimentam alertas (Fase 5) e narrativas (Fase 4)

**Entregáveis da Fase 3:**
- 2 entidades + tabelas novas
- 2 migrations EF Core
- 2 Hangfire jobs novos
- Detecção determinística + descrição em linguagem natural via z.ai
- Dados prontos para alimentar narrativas e alertas

---

## FASE 4 — Narrativa Semanal + Insights UI

**Objetivo:** Gerar resumo semanal com IA e exibir insights visuais ao usuário final.

**Issues:** CX-216, CX-217, CX-218

---

### 4.1 — Gerador de Narrativa Semanal via z.ai (CX-216)

**Job `WeeklyNarrativeJob`:**
- Schedule: domingo 23:00 UTC
- Gera para todos os usuários com ≥ 3 dias de dados na semana
- Cache: narrativa cacheada até a próxima semana (não regera)

**Contexto enviado ao z.ai:**

```json
{
  "period": "semana de 24 a 30 de março de 2026",
  "features": {
    "total_active_hours": 38.5,
    "avg_focus_score": 74,
    "avg_productive_ratio": 0.68,
    "trend_focus_score": 8,
    "trend_productive_ratio": 0.05
  },
  "patterns": [
    { "tag": "morning_productive", "strength": 0.9 },
    { "tag": "afternoon_focus_drop", "strength": 0.7 }
  ],
  "anomalies": [],
  "language": "pt-BR",
  "tone": "profissional e encorajador",
  "max_sentences": 4
}
```

**Saída esperada:**

> "Você trabalhou 38,5 horas esta semana, com seu foco 8 pontos acima da semana anterior. Suas manhãs continuam sendo seu período mais produtivo — aproveite esse padrão para alocar suas tarefas mais importantes antes do meio-dia. As tardes mostram uma queda consistente de atenção, o que pode ser mitigado com um bloco de foco estruturado entre 14h e 16h."

**Fallback:** Template determinístico se z.ai indisponível

**API:**

```
GET /api/v1/reports/weekly-narrative?userId={}&weekStart=YYYY-MM-DD
```

**Critérios de Aceite:**
- [ ] Narrativa gerada semanalmente para todos os usuários com ≥ 3 dias
- [ ] Tom profissional e encorajador (nunca acusatório)
- [ ] Fallback template funciona quando z.ai indisponível
- [ ] Resposta cacheada — segunda chamada não gera nova requisição z.ai
- [ ] Logado em `AiDecisionLog` com `tokens_used`

---

### 4.2 — Frontend: Card de Resumo Semanal no Dashboard (CX-217)

**Localização:** Dashboard → novo card na área principal

**Layout:**

```
┌─────────────────────────────────────────────────────────┐
│  Sua semana em resumo                     [👍 Útil] [👎] │
├─────────────────────────────────────────────────────────┤
│  Você trabalhou 38,5h esta semana, com foco 8 pontos    │
│  acima da semana anterior. Suas manhãs continuam sendo  │
│  seu período mais produtivo...                          │
│                                                         │
│  💡 Ativar modo foco das 14h às 16h nos próximos dias   │
└─────────────────────────────────────────────────────────┘
```

**Comportamentos:**
- Fetch automático ao entrar no Dashboard
- Loading skeleton enquanto carrega
- 👍/👎 → `POST /api/v1/ai/feedback`
- Card oculto se < 3 dias de dados na semana
- Card oculto para semanas futuras

**Critérios de Aceite:**
- [ ] Narrativa exibida com formatação adequada
- [ ] Feedback 👍/👎 enviado e registrado
- [ ] Loading skeleton durante fetch
- [ ] Card não aparece sem dados suficientes

---

### 4.3 — Frontend: Página de Insights (CX-218)

**Rota:** `/insights` (novo item no sidebar)

**5 Seções:**

**1. Seus Padrões Atuais**
- Cards com padrões ativos de `user_patterns`
- Ícone + nome + descrição z.ai + strength (barra de progresso)
- Empty state se sem padrões

**2. Tendência de Foco**
- Gráfico de linha (Recharts) com `avg_focus_score` das últimas 8 semanas
- Anotações nos pontos de anomalia (marcadores no gráfico)
- Dados de `feature_weekly`

**3. Mapa de Produtividade**
- Heatmap: eixo X = hora do dia (0-23h), eixo Y = dia da semana (Seg-Dom)
- Cor por `productivity_ratio` médio no slot
- Dados agregados de `DailyFocusScore`

**4. Benchmark com a Equipe**
- "Seu focus_score está X% acima/abaixo da média do time"
- Apenas médias agregadas, nunca dados individuais
- Dados de `feature_weekly` comparados com média da org

**5. Histórico de Resumos Semanais**
- Lista das últimas 12 narrativas com data
- Click para expandir e reler
- Dados do endpoint de narrativa

**API Endpoints necessários:**

```
GET /api/v1/insights/patterns           -- padrões ativos do usuário
GET /api/v1/insights/focus-trend        -- 8 semanas de feature_weekly
GET /api/v1/insights/productivity-map   -- heatmap data
GET /api/v1/insights/benchmark          -- comparação com média do time
GET /api/v1/insights/narratives/history -- últimas 12 narrativas
```

**Critérios de Aceite:**
- [ ] Todas as 5 seções renderizam corretamente
- [ ] Mapa de produtividade usa dados reais de DailyFocusScore
- [ ] Benchmark mostra apenas médias (nunca dados individuais)
- [ ] Histórico de narrativas navegável
- [ ] Empty state quando não há dados (< 2 semanas)

**Entregáveis da Fase 4:**
- 1 Hangfire job novo
- 5+ endpoints de API novos
- Card de resumo semanal no Dashboard
- Página `/insights` completa
- Feature de valor imediato para o usuário final

---

## FASE 5 — Alertas Inteligentes + Notificações

**Objetivo:** Alertas contextuais com thresholds adaptativos, entregues via central de notificações.

**Issues:** CX-219, CX-220

---

### 5.1 — Sistema de Alertas Contextuais (CX-219)

**Nova entidade `SmartAlert`:**

```sql
CREATE TABLE smart_alerts (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id         UUID NOT NULL,       -- destinatário
  about_user_id   UUID,                -- sobre quem (pode diferir)
  org_id          UUID NOT NULL,
  alert_type      TEXT NOT NULL,
  message         TEXT NOT NULL,       -- gerado pelo z.ai em PT-BR
  severity        TEXT NOT NULL,       -- 'info' | 'warning' | 'critical'
  action_type     TEXT,                -- 'start_pomodoro' | 'view_report' | null
  was_read        BOOLEAN DEFAULT false,
  was_acted       BOOLEAN DEFAULT false,
  created_at      TIMESTAMPTZ DEFAULT now()
);

CREATE INDEX idx_smart_alerts_user_unread
  ON smart_alerts (user_id, was_read, created_at DESC);
CREATE INDEX idx_smart_alerts_org_severity
  ON smart_alerts (org_id, severity, created_at DESC);
```

**4 tipos de alerta:**

| Destinatário | Trigger | Action Type |
|---|---|---|
| Colaborador | active_seconds > 10800 (3h sem pausa) | null |
| Colaborador | focus_score 2h < média_pessoal × 0.6 | `start_pomodoro` |
| Gestor | anomalia `productivity_drop` em membro | `view_report` |
| Admin | `high_context_switching` em > 30% do time | `view_report` |

**Thresholds adaptativos:**

```
Fixo (errado):      "Alerta se focus_score < 50"
Adaptativo (correto): "Alerta se focus_score < (média_pessoal_30d - 1.5×stddev)"
```

**Jobs:**

| Job | Schedule | Lógica |
|---|---|---|
| `AlertGenerationJob` | Após anomaly detection (ex: 02:00 UTC) | Gera alertas baseados em padrões e anomalias. Mensagem via `IAIService.GenerateAlertMessageAsync()` |
| `ThresholdUpdateJob` | Semanal (segunda 04:00 UTC) | Recalcula thresholds pessoais para cada usuário |

**Critérios de Aceite:**
- [ ] Alertas gerados com thresholds adaptativos por usuário
- [ ] Mensagem em PT-BR gerada pelo z.ai
- [ ] Severidade correta para cada tipo
- [ ] `about_user_id` preenchido quando alerta é sobre outro membro
- [ ] Action type preenchido para alertas com ação sugerida

---

### 5.2 — Frontend: Central de Notificações (CX-220)

**Localização:** Ícone de sino no header do Dashboard

**Layout:**

```
🔔 (3)
┌─────────────────────────────────────────────┐
│  Notificações         [Marcar todas lidas]  │
│  [Para mim] [Sobre meu time]                │
├─────────────────────────────────────────────┤
│  ⚠️ Você está há 3h sem pausa              │
│  há 12 minutos                              │
│  [Iniciar pausa]              [Dispensar]   │
├─────────────────────────────────────────────┤
│  📊 Sua semana em resumo está disponível    │
│  há 2 horas                                 │
│  [Ver insights]               [Dispensar]   │
└─────────────────────────────────────────────┘
⚙️ Configurar alertas
```

**Comportamentos:**
- Badge com contagem de não lidos (polling a cada 5 min)
- Tab "Para mim" / "Sobre meu team" (visível apenas para Gestor e Admin)
- Ação inline: "Iniciar Pomodoro" dispara IPC para o Agent
- "Ver Relatório" navega para `/insights` ou `/reports`
- Opt-out por tipo de alerta (salvo em preferências do usuário)
- Click fora fecha o painel

**API Endpoints:**

```
GET  /api/v1/alerts?filter=mine|team&unreadOnly=true
POST /api/v1/alerts/{id}/read
POST /api/v1/alerts/{id}/act
POST /api/v1/alerts/{id}/dismiss
GET  /api/v1/alerts/preferences
PUT  /api/v1/alerts/preferences
     Body: { "disabledTypes": ["absent_workday"] }
```

**Critérios de Aceite:**
- [ ] Badge atualiza via polling a cada 5 min
- [ ] Ações inline funcionam (Iniciar Pomodoro dispara IPC)
- [ ] Opt-out por tipo salvo em preferências
- [ ] Tab "Sobre meu time" visível apenas para Gestor e Admin
- [ ] Click fora fecha o painel

**Entregáveis da Fase 5:**
- 1 tabela nova + 2 Hangfire jobs
- Central de notificações no frontend
- Alertas contextuais adaptativos
- Integração com Pomodoro via IPC (desktop)

---

## FASE 6 — Feedback Loop + Métricas

**Objetivo:** Fechar o ciclo de melhoria contínua dos prompts e medir objetivamente o valor da IA.

**Issues:** CX-221, CX-222

---

### 6.1 — Pipeline de Feedback para Melhoria de Prompts (CX-221)

**Endpoint:**

```
POST /api/v1/ai/feedback
Body: {
  "decision_id": "uuid",
  "outcome": "accepted" | "rejected" | "corrected" | "useful" | "not_useful",
  "correct_value": { "category": "productive", "subcategory": "development" }
}
```

**Mapeamento de feedback:**

| Feedback | Ação no `AiDecisionLog` |
|---|---|
| Classificação aceita | `review_outcome = accepted` |
| Classificação corrigida | `review_outcome = corrected` + `correct_value` salvo |
| Classificação rejeitada | `review_outcome = rejected`, app sem sugestão por 30 dias |
| Narrativa 👍 | Registra padrões mencionados → enfatizar nos prompts |
| Narrativa 👎 | Registra padrões mencionados → reduzir peso nos prompts |

**Sem retreinamento** — toda melhoria é via refinamento de prompts e contexto enviado ao z.ai.

**Relatório semanal:**
- `GET /api/v1/internal/ai-feedback-report` (Admin only)
- Agregação semanal de feedbacks por tipo

**Critérios de Aceite:**
- [ ] Endpoint aceita todos os tipos de outcome
- [ ] `AiDecisionLog` atualizado com `review_outcome` e `correct_value`
- [ ] Apps rejeitados sem nova sugestão por 30 dias
- [ ] Relatório semanal disponível para Admin

---

### 6.2 — Métricas de Valor da IA (CX-222)

**Endpoint:**

```
GET /api/v1/internal/ai-metrics
(Apenas Admin / Superadmin → 403 para outros)
```

**Resposta:**

```json
{
  "classification": {
    "total_suggestions": 234,
    "accepted": 198,
    "corrected": 18,
    "rejected": 18,
    "accuracy_rate": 0.87
  },
  "narratives": {
    "total_generated": 156,
    "thumbs_up": 112,
    "thumbs_down": 44,
    "useful_rate": 0.72
  },
  "alerts": {
    "total_generated": 89,
    "read": 71,
    "acted": 37,
    "acted_rate": 0.42
  },
  "costs": {
    "total_tokens_used_month": 1240000,
    "avg_latency_ms": 847,
    "zai_calls_today": 23
  }
}
```

**Metas de sucesso da Fase 4:**

| Métrica | Meta | Ação se não atingida |
|---|---|---|
| classification accuracy_rate | > 0.85 | Revisar prompts de classificação |
| narratives useful_rate | > 0.70 | Revisar contexto enviado + tom |
| alerts acted_rate | > 0.35 | Revisar thresholds e severidade |

> Se após 4 semanas em produção alguma meta não for atingida → revisão de prompts e contexto antes de qualquer outro investimento.

**Critérios de Aceite:**
- [ ] Endpoint retorna todas as métricas listadas
- [ ] Dados calculados em tempo real sobre `AiDecisionLog`
- [ ] Acessível apenas para Admin/Superadmin
- [ ] Métricas de custo (tokens, latência) calculadas corretamente

**Entregáveis da Fase 6:**
- Pipeline de feedback completo (frontend → API → AiDecisionLog)
- Dashboard de métricas de IA para Admin
- Critérios objetivos de sucesso para revisão de prompts

---

## Resumo Geral

### Nova Tabelas / Migrações

| # | Tabela | Fase | Tipo |
|---|---|---|---|
| 1 | `daily_focus_scores` (extensão) | 1 | ALTER TABLE |
| 2 | `feature_weekly` | 1 | CREATE TABLE |
| 3 | `feature_monthly` | 1 | CREATE TABLE |
| 4 | `ai_decision_log` | 1 | CREATE TABLE |
| 5 | `user_patterns` | 3 | CREATE TABLE |
| 6 | `behavioral_anomalies` | 3 | CREATE TABLE |
| 7 | `smart_alerts` | 5 | CREATE TABLE |

### Novos Hangfire Jobs

| # | Job | Schedule | Fase |
|---|---|---|---|
| 1 | `FocusScoreJob` (atualizado) | Diário 00:05 UTC | 1 |
| 2 | `WeeklyFeatureAggregationJob` | Segunda 03:00 UTC | 1 |
| 3 | `MonthlyFeatureAggregationJob` | Dia 1 03:00 UTC | 1 |
| 4 | `AppClassificationJob` | Domingo 22:00 UTC | 2 |
| 5 | `PatternDetectionJob` | Diário 01:00 UTC | 3 |
| 6 | `AnomalyDetectionJob` | Diário 01:30 UTC | 3 |
| 7 | `WeeklyNarrativeJob` | Domingo 23:00 UTC | 4 |
| 8 | `AlertGenerationJob` | Diário 02:00 UTC | 5 |
| 9 | `ThresholdUpdateJob` | Segunda 04:00 UTC | 5 |

### Novos Projetos / Módulos

| Projeto | Fase | Propósito |
|---|---|---|
| `TimeTrack.Backend.AI` | 1 | Cliente z.ai, resiliência, fallback |

### Complexidade Estimada por Fase

| Fase | Backend | Frontend | Risco |
|---|---|---|---|
| 1 | Pesado | Nenhum | Médio (z.ai contract) |
| 2 | Médio | Médio | Baixo |
| 3 | Médio | Nenhum | Baixo |
| 4 | Médio | Pesado | Médio (UI complexa) |
| 5 | Médio | Médio | Médio (IPC integration) |
| 6 | Leve | Nenhum | Baixo |

### Ordem de Dependência

```
Fase 1 (fundação)
  ├── Fase 2 (classificação apps) → depende de IAIService
  ├── Fase 3 (padrões/anomalias) → depende de DailyFocusScore extendido
  │     ├── Fase 4 (narrativa/insights) → depende de padrões + features
  │     └── Fase 5 (alertas) → depende de anomalias + padrões
  └── Fase 6 (feedback/métricas) → depende de AiDecisionLog populado
```

> Fases 2, 3 e 6 podem rodar em paralelo após Fase 1.
> Fases 4 e 5 dependem da Fase 3 estarem completas.
