# ADR-0005: Hangfire para Jobs em Background

## Status

Accepted

## Context

O Backend precisa executar tarefas em background:
- **Agregação diária**: Consolidar dados de tracking
- **Limpeza de dados**: Remover dados antigos (retenção)
- **Relatórios agendados**: Gerar relatórios periódicos
- **Notificações**: Enviar emails/lembretes

Requisitos:
- Execução agendada (diário, semanal)
- Retry automático em falhas
- Visibilidade do status dos jobs
- Persistência entre restarts

Alternativas consideradas:
1. **Hangfire** - Dashboard built-in, persistência automática
2. **Quartz.NET** - Mais flexível, sem dashboard
3. **Azure Functions** - Serverless, mas requer infra cloud
4. **Custom IHostedService** - Simples mas sem retry/dashboard

## Decision

Escolhemos **Hangfire** para gerenciamento de jobs em background:

### Arquitetura

```
┌─────────────────────────────────────────────────────────────┐
│                      BACKEND API                             │
│                                                              │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐  │
│  │ Controllers  │───▶│  Hangfire    │───▶│  PostgreSQL  │  │
│  │              │    │  Server      │    │  (Storage)   │  │
│  └──────────────┘    └──────────────┘    └──────────────┘  │
│                             │                                │
│                             ▼                                │
│                      ┌──────────────┐                       │
│                      │   Dashboard  │                       │
│                      │  /hangfire   │                       │
│                      └──────────────┘                       │
└─────────────────────────────────────────────────────────────┘
```

### Jobs Configurados

```csharp
public static class HangfireConfiguration
{
    public static void ConfigureRecurringJobs()
    {
        // Agregação diária - 2:00 AM UTC
        RecurringJob.AddOrUpdate<DailyAggregationJob>(
            "daily-aggregation",
            job => job.ExecuteAsync(),
            "0 2 * * *");

        // Limpeza de retenção - Domingo 3:00 AM UTC
        RecurringJob.AddOrUpdate<RetentionCleanupJob>(
            "retention-cleanup",
            job => job.ExecuteAsync(),
            "0 3 * * 0");

        // Sync de focus scores - A cada hora
        RecurringJob.AddOrUpdate<FocusScoreSyncJob>(
            "focus-score-sync",
            job => job.ExecuteAsync(),
            "0 * * * *");
    }
}
```

### Dashboard

Acesso em `/hangfire` com autorização por role Admin:
- Visualizar jobs em execução
- Histórico de execuções
- Retries automáticos
- Métricas de performance

## Consequences

### Positivas

- **Dashboard built-in**: Visibilidade sem código extra
- **Persistência**: Jobs sobrevivem a restarts
- **Retry automático**: Configurável por job
- **Fácil integração**: Atributos e DI support
- **Escalável**: Múltiplos workers se necessário

### Negativas

- **Storage overhead**: Tabelas adicionais no banco
- **Polling**: Consulta periódica ao banco para jobs
- **Lock contention**: Jobs longos podem bloquear outros

### Mitigações

- **Connection pooling**: Reutilizar conexões
- **Job timeout**: Configurar timeout para evitar jobs presos
- **Queue isolation**: Filas separadas para jobs críticos
- **Monitoring**: Alertas para jobs com muitas falhas

## Implementation Notes

### Configuração

```csharp
// Program.cs
builder.Services.AddHangfire(config =>
    config.UsePostgreSqlStorage(
        c => c.ConnectionString(connectionString)));

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 5;
    options.ServerTimeout = TimeSpan.FromMinutes(5);
    options.SchedulePollingInterval = TimeSpan.FromSeconds(15);
});

// Dashboard com autorização
app.MapHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireDashboardAuthorizationFilter() },
    DashboardTitle = "TimeTrack Jobs Dashboard"
});
```

### Job Example

```csharp
public class DailyAggregationJob
{
    private readonly ILogger<DailyAggregationJob> _logger;
    private readonly IAggregationService _aggregationService;

    public DailyAggregationJob(
        ILogger<DailyAggregationJob> logger,
        IAggregationService aggregationService)
    {
        _logger = logger;
        _aggregationService = aggregationService;
    }

    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    [JobDisplayName("Daily Aggregation for {0}")]
    public async Task ExecuteAsync()
    {
        _logger.LogInformation("Starting daily aggregation job");

        var yesterday = DateTime.UtcNow.Date.AddDays(-1);
        await _aggregationService.AggregateDayAsync(yesterday);

        _logger.LogInformation("Daily aggregation completed");
    }
}
```

## References

- [Hangfire Documentation](https://docs.hangfire.io/)
- [Hangfire PostgreSQL Storage](https://github.com/frankhommers/Hangfire.PostgreSql)
