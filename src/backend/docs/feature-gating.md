# Feature Gating — Guia de Referência

## Visão Geral

O sistema de feature gating controla quais funcionalidades cada plano de assinatura oferece. Funciona em 3 camadas:

1. **Config** (`appsettings.json`) — Fonte de verdade dos valores (true/false por plano)
2. **Backend Middleware** — Bloqueia endpoints com `[RequireSubscription("FeatureName")]` retornando 403
3. **Frontend Hook** — `useFeature('featureName')` mostra/esconde UI

---

## Arquitetura

```
appsettings.json (Plans → Features)
    ↓ startup (seed via reflection)
Database (subscription_plans boolean columns)
    ↓ middleware (reflection)
[RequireSubscription("AiAccess")] → 403 se desabilitado
    ↓ API response
GET /billing/subscription → { features: { flags: { aiAccess: true } } }
    ↓ Zustand store
useFeature('aiAccess') → true/false no componente
```

---

## Checklist: Como adicionar uma nova feature

### Exemplo: Adicionar "AiAccess"

#### 1. `SubscriptionPlan.cs` (Domain)
```csharp
// Adicionar propriedade booleana
public bool AiAccess { get; private set; }
```
O FeatureMap (via reflection) detecta automaticamente.

#### 2. `PlansOptions.cs` (Infrastructure)
```csharp
// Adicionar na classe PlanFeatures
public bool AiAccess { get; init; }
```
Para o config binding do `appsettings.json`.

#### 3. `appsettings.json`
```json
{
  "Plans": [
    {
      "Tier": "Pro",
      "Features": {
        "AiAccess": false
      }
    },
    {
      "Tier": "Enterprise",
      "Features": {
        "AiAccess": true
      }
    }
  ]
}
```

#### 4. `SubscriptionPlanConfiguration.cs` (EF)
```csharp
builder.Property(p => p.AiAccess).HasDefaultValue(false);
```

#### 5. Migration
```bash
dotnet ef migrations add AddAiAccess --project TimeTrack.Backend.Infrastructure --startup-project TimeTrack.Api
```

#### 6. Reiniciar a API
O seed sincroniza automaticamente `appsettings.json` → banco via reflection.

### O que NÃO precisa mexer (automático via reflection):
- ~~FeatureMap~~ — automático
- ~~SubscriptionPlanSeed~~ — automático
- ~~BillingDTOs~~ — automático via `ToFeatureDictionary()`
- ~~GetSubscriptionStatusQuery~~ — automático
- ~~GetAvailablePlansQuery~~ — automático

---

## Uso no Backend

### Bloquear endpoint inteiro
```csharp
[HttpGet("api/v1/ai/analyze")]
[RequireSubscription("AiAccess")]  // Retorna 403 se o plano não tem
public async Task<ActionResult> Analyze() { ... }
```

### Verificar programaticamente
```csharp
// Injetar ISubscriptionService
var enabled = await _subscriptionService.IsFeatureEnabledAsync(orgId, "AiAccess", ct);
if (!enabled) throw new FeatureNotEnabledException("AiAccess");
```

### Respostas do middleware

| Cenário | HTTP | Body |
|---------|------|------|
| Sem assinatura | 402 | `{ code: "subscription_required" }` |
| Assinatura ok, feature desabilitada | 403 | `{ code: "feature_not_enabled" }` |
| Assinatura ok, feature habilitada | 200 | — |
| Feature name desconhecido (typo) | 200 | Loga warning, não bloqueia |

---

## Uso no Frontend

### Hook: useFeature
```typescript
import { useFeature } from '../hooks/useFeature';

function AiPanel() {
  const canUseAi = useFeature('aiAccess');

  if (!canUseAi) {
    return <UpsellBanner feature="AI Analysis" />;
  }

  return <AiAnalyzer />;
}
```

### Store: subscriptionStore
```typescript
import { useSubscriptionStore } from '../stores/subscriptionStore';

// Popular no mount do layout autenticado
useEffect(() => {
  useSubscriptionStore.getState().fetchSubscription();
}, []);

// Limpar no logout (já integrado no authStore)
```

---

## Arquivos Envolvidos

| Arquivo | Responsabilidade |
|---------|-----------------|
| `Domain/Entities/SubscriptionPlan.cs` | Entidade + FeatureMap (reflection) |
| `Api/Middleware/SubscriptionCheckMiddleware.cs` | Enforcement (402/403) |
| `Infrastructure/Services/SubscriptionService.cs` | `IsFeatureEnabledAsync()` |
| `Infrastructure/Integrations/Stripe/PlansOptions.cs` | Config binding |
| `Infrastructure/Persistence/Seeds/SubscriptionPlanSeed.cs` | Config → DB (reflection) |
| `Infrastructure/Persistence/Configurations/SubscriptionPlanConfiguration.cs` | EF column mapping |
| `Application/Billing/DTOs/BillingDTOs.cs` | `PlanFeatureSet` (Dictionary) |
| `Application/Billing/Queries/GetSubscriptionStatusQuery.cs` | Features na API response |
| `Api/appsettings.json` | Fonte de verdade dos valores |
| `ui/stores/subscriptionStore.ts` | Zustand store de features |
| `ui/hooks/useFeature.ts` | Hook de verificação |
| `ui/types/billing.ts` | `PlanFeatureSet = Record<string, boolean>` |

---

## Testes

Localização: `TimeTrack.Backend.Tests/Billing/SubscriptionPlanFeatureTests.cs`

| Teste | Valida |
|-------|--------|
| `IsFeatureEnabled_ReturnsTrue_WhenFeatureIsEnabled` | Feature true funciona |
| `IsFeatureEnabled_ReturnsFalse_WhenFeatureIsDisabled` | Feature false funciona |
| `IsFeatureEnabled_ReturnsFalse_ForUnknownFeature` | Feature inexistente = false |
| `IsFeatureEnabled_IsCaseInsensitive` | Case-insensitive |
| `IsValidFeatureName_ReturnsTrue_ForAllKnownFeatures` | Todas features reconhecidas |
| `ToFeatureDictionary_ReturnsAllFeatures` | Dictionary completo |
| `FeatureMap_AutoDiscoversBooleanProperties` | Reflection funciona |
| `Create_WithUnknownFeatureKeys_IgnoresThemGracefully` | Keys inexistentes não quebram |
