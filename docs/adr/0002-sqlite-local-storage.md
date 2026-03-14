# ADR-0002: SQLite para Armazenamento Local do Agent

## Status

Accepted

## Context

O Agent roda como Windows Service no desktop do usuário e precisa:
- Armazenar sessões de tracking localmente
- Funcionar offline (sem conexão com backend)
- Sincronizar dados quando conectividade retornar
- Ter baixo impacto em CPU e memória
- Ser simples de instalar (sem dependências extras)

Alternativas consideradas:
1. **SQLite** - Banco embarcado, arquivo único
2. **SQL Server Express** - Requer instalação separada
3. **JSON files** - Simples mas sem queries
4. **Embedded PostgreSQL** - Muito pesado para desktop

## Decision

Escolhemos **SQLite** como banco de dados local do Agent:

### Configuração

```json
// appsettings.json
{
  "Agent": {
    "DatabasePath": "timetrack.db"
  }
}
```

### Estrutura de Tabelas

- `sessions` - Sessões de trabalho
- `window_events` - Eventos de janela ativa
- `outbox_messages` - Mensagens pendentes para sync
- `focus_sessions` - Sessões de focus mode

### ORM

Usamos **Entity Framework Core** com provider `Microsoft.Data.Sqlite`:
- Code-first migrations
- LINQ queries
- Change tracking automático

## Consequences

### Positivas

- **Zero configuração**: Arquivo único, sem instalação
- **Performance**: Operações em memória, I/O mínimo
- **Offline-first**: Funciona sem rede
- **Portabilidade**: Arquivo pode ser copiado/movido
- **Baixo footprint**: < 10MB RAM, < 1% CPU idle

### Negativas

- **Concorrência limitada**: Um writer por vez
- **Sem replicação nativa**: Sync manual com backend
- **Lock em escritas**: Pode bloquear durante batch inserts

### Mitigações

- **Write batching**: Agrupar escritas em transações
- **WAL mode**: Write-Ahead Logging para melhor concorrência
- **Outbox pattern**: Desacoplar escrita local de sync remoto
- **Timeouts adequados**: Configurar `CommandTimeout` para operações longas

## Implementation Notes

```csharp
// Configuração do SQLite com WAL mode
protected override void OnConfiguring(DbContextOptionsBuilder options)
{
    options.UseSqlite(connectionString, options =>
    {
        options.CommandTimeout(30);
    });

    // Habilitar WAL mode para melhor concorrência
    var connection = Database.GetDbConnection();
    connection.Open();
    using var cmd = connection.CreateCommand();
    cmd.CommandText = "PRAGMA journal_mode=WAL;";
    cmd.ExecuteNonQuery();
}
```

## References

- [SQLite Documentation](https://www.sqlite.org/docs.html)
- [EF Core SQLite Provider](https://docs.microsoft.com/en-us/ef/core/providers/sqlite/)
- [SQLite WAL Mode](https://www.sqlite.org/wal.html)
