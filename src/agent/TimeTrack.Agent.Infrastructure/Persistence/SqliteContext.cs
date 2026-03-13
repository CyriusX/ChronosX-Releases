using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace TimeTrack.Agent.Infrastructure.Persistence;

/// <summary>
/// Contexto SQLite para persistência local do Agent
/// </summary>
public sealed class SqliteContext : IAsyncDisposable
{
    private readonly ILogger<SqliteContext> _logger;
    private readonly string _connectionString;
    private SqliteConnection? _connection;
    private bool _initialized;

    public SqliteContext(string databasePath, ILogger<SqliteContext> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("Database path is required", nameof(databasePath));

        _connectionString = $"Data Source={databasePath}";
    }

    /// <summary>
    /// Obtém a conexão SQLite (cria se necessário)
    /// </summary>
    public async Task<SqliteConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_connection == null)
        {
            _connection = new SqliteConnection(_connectionString);
            await _connection.OpenAsync(cancellationToken);
        }

        return _connection;
    }

    /// <summary>
    /// Inicializa o schema do banco de dados
    /// </summary>
    public async Task InitializeSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;

        var connection = await GetConnectionAsync(cancellationToken);

        // Configurar PRAGMAs para performance
        await connection.ExecuteAsync("PRAGMA journal_mode = WAL");
        await connection.ExecuteAsync("PRAGMA synchronous = NORMAL");
        await connection.ExecuteAsync("PRAGMA busy_timeout = 5000");

        // Criar tabelas (sem user_id inicialmente para compatibilidade)
        var createTablesSql = @"
            -- Tabela de estado do tracking
            CREATE TABLE IF NOT EXISTS tracking_state (
                id TEXT PRIMARY KEY,
                user_id TEXT,
                status INTEGER NOT NULL,
                reason TEXT,
                paused_at TEXT,
                resumed_at TEXT,
                updated_at TEXT NOT NULL,
                last_modified_by TEXT
            );

            -- Tabela de sessões de atividade
            CREATE TABLE IF NOT EXISTS activity_sessions (
                id TEXT PRIMARY KEY,
                user_id TEXT,
                exe_path_hash TEXT NOT NULL,
                display_name TEXT NOT NULL,
                category_productivity TEXT NOT NULL,
                category_subcategory TEXT NOT NULL,
                category_source TEXT NOT NULL,
                start_utc TEXT NOT NULL,
                end_utc TEXT NOT NULL,
                window_hash TEXT,
                window_title TEXT,
                created_at TEXT NOT NULL DEFAULT (datetime('now'))
            );

            -- Tabela de períodos de inatividade
            CREATE TABLE IF NOT EXISTS idle_periods (
                id TEXT PRIMARY KEY,
                user_id TEXT,
                start_utc TEXT NOT NULL,
                end_utc TEXT NOT NULL,
                threshold_seconds INTEGER NOT NULL,
                is_system_detected INTEGER NOT NULL DEFAULT 1,
                created_at TEXT NOT NULL DEFAULT (datetime('now'))
            );

            -- Tabela sync_outbox (Transactional Outbox Pattern)
            CREATE TABLE IF NOT EXISTS sync_outbox (
                id TEXT PRIMARY KEY,
                user_id TEXT,
                entity_type TEXT NOT NULL,
                entity_id TEXT NOT NULL,
                payload_json TEXT NOT NULL,
                idempotency_key TEXT NOT NULL UNIQUE,
                attempt_count INTEGER NOT NULL DEFAULT 0,
                next_attempt_utc TEXT NOT NULL,
                sent_at TEXT,
                last_error TEXT,
                created_at TEXT NOT NULL DEFAULT (datetime('now'))
            );

            -- Tabela de erros de sincronização
            CREATE TABLE IF NOT EXISTS sync_errors (
                id TEXT PRIMARY KEY,
                user_id TEXT,
                timestamp_utc TEXT NOT NULL,
                endpoint TEXT NOT NULL,
                status_code INTEGER NOT NULL,
                error_message TEXT NOT NULL,
                attempt_count INTEGER NOT NULL DEFAULT 1
            );

            -- Tabela de configurações locais (preferências do colaborador)
            CREATE TABLE IF NOT EXISTS local_settings (
                id TEXT PRIMARY KEY,
                user_id TEXT,
                auto_resume_notification_enabled INTEGER NOT NULL DEFAULT 1,
                notification_sounds_enabled INTEGER NOT NULL DEFAULT 1,
                language TEXT NOT NULL DEFAULT 'pt-BR',
                updated_at TEXT NOT NULL
            );

            -- Tabela de ciclos de foco (Pomodoro/Ultradian)
            CREATE TABLE IF NOT EXISTS focus_cycles (
                id TEXT PRIMARY KEY,
                user_id TEXT,
                mode INTEGER NOT NULL,
                cycle_number INTEGER NOT NULL,
                started_at TEXT NOT NULL,
                ended_at TEXT,
                planned_ms INTEGER NOT NULL,
                actual_ms INTEGER,
                completed INTEGER NOT NULL DEFAULT 0,
                break_taken INTEGER NOT NULL DEFAULT 0,
                synced INTEGER NOT NULL DEFAULT 0,
                date TEXT NOT NULL
            );
        ";

        await connection.ExecuteAsync(createTablesSql);

        // Executar migrations (adicionar colunas se não existirem)
        await RunMigrationsAsync(connection);

        // Criar índices
        await CreateIndexesAsync(connection);

        _initialized = true;
        _logger.LogInformation("SQLite schema initialized successfully with WAL mode");
    }

    /// <summary>
    /// Executa migrations para adicionar colunas user_id se não existirem
    /// </summary>
    private async Task RunMigrationsAsync(SqliteConnection connection)
    {
        var tables = new[]
        {
            "tracking_state",
            "activity_sessions",
            "idle_periods",
            "sync_outbox",
            "sync_errors",
            "local_settings",
            "focus_cycles"
        };

        foreach (var table in tables)
        {
            // Verificar se a coluna user_id existe
            var columnExists = await connection.QueryFirstOrDefaultAsync<int>(
                "SELECT COUNT(*) FROM pragma_table_info(@Table) WHERE name = 'user_id'",
                new { Table = table });

            if (columnExists == 0)
            {
                _logger.LogInformation("Adding user_id column to {Table}", table);
                await connection.ExecuteAsync($"ALTER TABLE {table} ADD COLUMN user_id TEXT");
            }
        }
    }

    /// <summary>
    /// Cria índices para consultas comuns
    /// </summary>
    private async Task CreateIndexesAsync(SqliteConnection connection)
    {
        var createIndexesSql = @"
            CREATE INDEX IF NOT EXISTS ix_tracking_state_user_id ON tracking_state(user_id);
            CREATE INDEX IF NOT EXISTS ix_activity_sessions_user_id ON activity_sessions(user_id);
            CREATE INDEX IF NOT EXISTS ix_activity_sessions_start_utc ON activity_sessions(start_utc);
            CREATE INDEX IF NOT EXISTS ix_activity_sessions_exe_path_hash ON activity_sessions(exe_path_hash);
            CREATE INDEX IF NOT EXISTS ix_idle_periods_user_id ON idle_periods(user_id);
            CREATE INDEX IF NOT EXISTS ix_idle_periods_start_utc ON idle_periods(start_utc);
            CREATE INDEX IF NOT EXISTS ix_sync_outbox_user_id ON sync_outbox(user_id);
            CREATE INDEX IF NOT EXISTS ix_sync_outbox_next_attempt ON sync_outbox(next_attempt_utc);
            CREATE INDEX IF NOT EXISTS ix_sync_outbox_entity_type_entity_id ON sync_outbox(entity_type, entity_id);
            CREATE INDEX IF NOT EXISTS ix_sync_errors_user_id ON sync_errors(user_id);
            CREATE INDEX IF NOT EXISTS ix_sync_errors_timestamp ON sync_errors(timestamp_utc);
            CREATE INDEX IF NOT EXISTS ix_local_settings_user_id ON local_settings(user_id);
            CREATE INDEX IF NOT EXISTS ix_focus_cycles_user_id ON focus_cycles(user_id);
            CREATE INDEX IF NOT EXISTS ix_focus_cycles_date ON focus_cycles(date);
            CREATE INDEX IF NOT EXISTS ix_focus_cycles_started_at ON focus_cycles(started_at);
        ";

        await connection.ExecuteAsync(createIndexesSql);
    }

    /// <summary>
    /// Migra registros órfãos (sem user_id) para o usuário especificado
    /// Deve ser chamado quando o usuário faz login
    /// </summary>
    public async Task MigrateOrphanRecordsToUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        var userIdStr = userId.ToString();

        var tables = new[]
        {
            "tracking_state",
            "activity_sessions",
            "idle_periods",
            "sync_outbox",
            "sync_errors",
            "local_settings"
        };

        foreach (var table in tables)
        {
            var updated = await connection.ExecuteAsync(
                $"UPDATE {table} SET user_id = @UserId WHERE user_id IS NULL",
                new { UserId = userIdStr });

            if (updated > 0)
            {
                _logger.LogInformation(
                    "Migrated {Count} orphan records in {Table} to user {UserId}",
                    updated, table, userId);
            }
        }
    }

    /// <summary>
    /// Inicia uma transação SQLite
    /// </summary>
    public async Task<SqliteTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        return (SqliteTransaction)await connection.BeginTransactionAsync();
    }

    /// <summary>
    /// Executa múltiplas comandos dentro de uma transação
    /// </summary>
    public async Task ExecuteInTransactionAsync(
        SqliteTransaction transaction,
        string sql,
        object? param = null,
        CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        await Dapper.SqlMapper.ExecuteAsync(connection, sql, param, transaction);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}

/// <summary>
/// Extensão para Dapper (método ExecuteAsync wrapper)
/// </summary>
internal static class SqliteConnectionExtensions
{
    public static async Task<int> ExecuteAsync(
        this SqliteConnection connection,
        string sql,
        object? param = null)
    {
        return await Dapper.SqlMapper.ExecuteAsync(connection, sql, param);
    }

    public static async Task<IEnumerable<T>> QueryAsync<T>(
        this SqliteConnection connection,
        string sql,
        object? param = null)
    {
        return await Dapper.SqlMapper.QueryAsync<T>(connection, sql, param);
    }

    public static async Task<T?> QueryFirstOrDefaultAsync<T>(
        this SqliteConnection connection,
        string sql,
        object? param = null)
    {
        return await Dapper.SqlMapper.QueryFirstOrDefaultAsync<T>(connection, sql, param);
    }
}
