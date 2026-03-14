# ADR-0004: Outbox Pattern para Sync Confiável

## Status

Accepted

## Context

O Agent precisa sincronizar dados de tracking com o Backend cloud:
- Eventos de janela ativa (frequentes)
- Sessões consolidadas (periódicas)
- Métricas de focus mode

Desafios:
- **Conectividade instável**: Usuário pode ficar offline
- **Falhas de rede**: Timeouts, erros HTTP
- **Ordem de eventos**: Algumas operações dependem de anteriores
- **Idempotência**: Reenvios não podem duplicar dados

Alternativas consideradas:
1. **Sync direto** - Simples mas perde dados em falhas
2. **Queue em memória** - Perde dados em restart
3. **Outbox pattern** - Persiste antes de enviar
4. **Event sourcing** - Overkill para necessidade atual

## Decision

Implementamos o **Outbox Pattern**:

### Arquitetura

```
┌─────────────────────────────────────────────────────────────┐
│                        AGENT                                 │
│                                                              │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐  │
│  │  Use Cases   │───▶│   SQLite     │───▶│   Outbox     │  │
│  │  (Domain)    │    │   (Local DB) │    │   Table      │  │
│  └──────────────┘    └──────────────┘    └──────────────┘  │
│                                                   │          │
│                                                   ▼          │
│                                          ┌──────────────┐   │
│                                          │ SyncWorker   │   │
│                                          │ (Background) │   │
│                                          └──────────────┘   │
└───────────────────────────────────────────┼─────────────────┘
                                            │
                                            ▼ HTTPS
                                    ┌──────────────┐
                                    │   Backend    │
                                    │   API        │
                                    └──────────────┘
```

### Tabela Outbox

```sql
CREATE TABLE outbox_messages (
    id TEXT PRIMARY KEY,
    aggregate_type TEXT NOT NULL,    -- 'Session', 'WindowEvent'
    aggregate_id TEXT NOT NULL,
    event_type TEXT NOT NULL,        -- 'Created', 'Updated'
    payload TEXT NOT NULL,           -- JSON
    created_at TEXT NOT NULL,
    sent_at TEXT,
    error_message TEXT,
    retry_count INTEGER DEFAULT 0,
    max_retries INTEGER DEFAULT 5
);
```

### Fluxo

1. **Write**: Use case salva entidade + mensagem no outbox (mesma transação)
2. **Read**: SyncWorker busca mensagens não enviadas
3. **Send**: Batch de mensagens para Backend
4. **Ack**: Marca como enviado após confirmação
5. **Retry**: Em caso de erro, incrementa contador e tenta depois

## Consequences

### Positivas

- **Reliability**: Dados persistidos localmente antes de enviar
- **Offline support**: Mensagens acumulam quando offline
- **At-least-once**: Garantia de entrega (pode duplicar)
- **Auditability**: Histórico de envios e erros
- **Backpressure**: Controle de fluxo com batch size

### Negativas

- **Latência**: Dados não são instantaneamente no backend
- **Storage**: Acúmulo de mensagens em offline prolongado
- **Complexidade**: Tabela adicional e worker de sync
- **Duplicates**: Backend precisa ser idempotente

### Mitigações

- **Batch sync**: Agrupar mensagens para eficiência
- **Cleanup**: Remover mensagens enviadas após X dias
- **Alerting**: Notificar se muitas mensagens pendentes
- **Idempotency keys**: Backend usa ID para deduplicar

## Implementation Notes

### Salvando no Outbox

```csharp
public async Task<Result> ConsolidateSessionAsync(ConsolidateSessionRequest request)
{
    await using var transaction = await _context.Database.BeginTransactionAsync();

    try
    {
        // 1. Salvar sessão
        _context.Sessions.Add(session);
        await _context.SaveChangesAsync();

        // 2. Criar mensagem outbox
        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid().ToString(),
            AggregateType = "Session",
            AggregateId = session.Id,
            EventType = "Consolidated",
            Payload = JsonSerializer.Serialize(session),
            CreatedAt = DateTime.UtcNow
        };
        _context.OutboxMessages.Add(outboxMessage);
        await _context.SaveChangesAsync();

        await transaction.CommitAsync();
        return Result.Success();
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}
```

### SyncWorker

```csharp
public class SyncWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var messages = await _context.OutboxMessages
                .Where(m => m.SentAt == null && m.RetryCount < m.MaxRetries)
                .OrderBy(m => m.CreatedAt)
                .Take(_config.MaxBatchSize)
                .ToListAsync(ct);

            if (messages.Any())
            {
                await SendBatchAsync(messages, ct);
            }

            await Task.Delay(_config.SyncInterval, ct);
        }
    }
}
```

## References

- [Outbox Pattern - microservices.io](https://microservices.io/patterns/data/transactional-outbox.html)
- [Reliable Delivery - Enterprise Integration Patterns](https://www.enterpriseintegrationpatterns.com/patterns/messaging/MessageConsumer.html)
