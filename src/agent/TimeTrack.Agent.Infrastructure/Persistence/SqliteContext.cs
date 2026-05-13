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
    private readonly SemaphoreSlim _lock = new(1, 1);
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
    /// Obtém a conexão SQLite (cria ou recria se necessário)
    /// </summary>
    public async Task<SqliteConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (IsConnectionBroken())
            {
                _logger.LogWarning("SQLite connection was broken, reconnecting");
                await DisposeConnectionCoreAsync();

                _connection = new SqliteConnection(_connectionString);
                await _connection.OpenAsync(cancellationToken);
                _initialized = false;
                await InitializeSchemaAsync(cancellationToken);
            }
            else if (_connection == null)
            {
                _connection = new SqliteConnection(_connectionString);
                await _connection.OpenAsync(cancellationToken);
            }

            return _connection;
        }
        finally
        {
            _lock.Release();
        }
    }

    private bool IsConnectionBroken()
    {
        if (_connection == null) return false;

        try
        {
            return _connection.State != System.Data.ConnectionState.Open;
        }
        catch (ObjectDisposedException)
        {
            return true;
        }
    }

    private async Task DisposeConnectionCoreAsync()
    {
        if (_connection != null)
        {
            try
            {
                await _connection.CloseAsync();
                await _connection.DisposeAsync();
            }
            catch (ObjectDisposedException) { }
            _connection = null;
        }
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
                file_path TEXT,
                domain TEXT,
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
                justification_state TEXT NOT NULL DEFAULT 'none',
                justification_reason_code TEXT,
                justification_note TEXT,
                justification_submitted_at_utc TEXT,
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

            -- Cache de políticas da organização (fonte de verdade para idle threshold)
            CREATE TABLE IF NOT EXISTS org_policies_cache (
                org_id TEXT PRIMARY KEY,
                idle_threshold_seconds INTEGER NOT NULL,
                idle_justification_prompt_threshold_seconds INTEGER,
                version INTEGER NOT NULL,
                updated_at TEXT NOT NULL,
                screenshots_enabled INTEGER NOT NULL DEFAULT 0,
                screenshot_interval_minutes INTEGER NOT NULL DEFAULT 5,
                screenshot_excluded_apps_json TEXT NOT NULL DEFAULT '[]',
                evidence_retention_days INTEGER NOT NULL DEFAULT 30,
                website_tracking_enabled INTEGER NOT NULL DEFAULT 1
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

            -- Tabela de eventos do agent (ações do usuário, eventos de sistema, erros)
            CREATE TABLE IF NOT EXISTS agent_event_log (
                id TEXT PRIMARY KEY,
                event_type TEXT NOT NULL,
                category TEXT NOT NULL,
                severity TEXT NOT NULL,
                message TEXT NOT NULL,
                metadata_json TEXT,
                timestamp_utc TEXT NOT NULL DEFAULT (datetime('now'))
            );

            -- CX-143: Tabela de cache de categorias de apps
            CREATE TABLE IF NOT EXISTS app_category_cache (
                id TEXT PRIMARY KEY,
                identifier TEXT NOT NULL UNIQUE,
                identifier_type TEXT NOT NULL,
                display_name TEXT NOT NULL,
                productivity TEXT NOT NULL,
                subcategory TEXT NOT NULL,
                source TEXT NOT NULL,
                version INTEGER NOT NULL,
                cached_at TEXT NOT NULL,
                note TEXT
            );

            -- F2-E2: Fila de upload de evidências (screenshots)
            CREATE TABLE IF NOT EXISTS evidence_upload_queue (
                id TEXT PRIMARY KEY,
                local_path TEXT NOT NULL,
                evidence_type TEXT NOT NULL DEFAULT 'screenshot',
                captured_at TEXT NOT NULL,
                app_name TEXT NOT NULL,
                window_title_hash TEXT,
                attempt_count INTEGER NOT NULL DEFAULT 0,
                next_attempt_utc TEXT NOT NULL,
                uploaded_at TEXT,
                file_size_bytes INTEGER NOT NULL DEFAULT 0,
                status TEXT NOT NULL DEFAULT 'pending',
                created_at TEXT NOT NULL DEFAULT (datetime('now'))
            );

            -- CX-143: Tabela de metadados de cache
            CREATE TABLE IF NOT EXISTS cache_info (
                cache_type TEXT PRIMARY KEY,
                version INTEGER NOT NULL DEFAULT 0,
                last_sync TEXT NOT NULL
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

        // Add domain column to activity_sessions (browser URL domain tracking)
        var domainExists = await connection.QueryFirstOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM pragma_table_info('activity_sessions') WHERE name = 'domain'");

        if (domainExists == 0)
        {
            _logger.LogInformation("Adding domain column to activity_sessions");
            await connection.ExecuteAsync("ALTER TABLE activity_sessions ADD COLUMN domain TEXT");
        }

        // Add file_path column to activity_sessions (file/folder path tracking)
        var filePathExists = await connection.QueryFirstOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM pragma_table_info('activity_sessions') WHERE name = 'file_path'");

        if (filePathExists == 0)
        {
            _logger.LogInformation("Adding file_path column to activity_sessions");
            await connection.ExecuteAsync("ALTER TABLE activity_sessions ADD COLUMN file_path TEXT");
        }

        // Add idle_threshold_seconds column to local_settings
        var idleThresholdExists = await connection.QueryFirstOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM pragma_table_info('local_settings') WHERE name = 'idle_threshold_seconds'");

        if (idleThresholdExists == 0)
        {
            _logger.LogInformation("Adding idle_threshold_seconds column to local_settings");
            await connection.ExecuteAsync("ALTER TABLE local_settings ADD COLUMN idle_threshold_seconds INTEGER");
        }

        var idleJustificationStateExists = await connection.QueryFirstOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM pragma_table_info('idle_periods') WHERE name = 'justification_state'");

        if (idleJustificationStateExists == 0)
        {
            _logger.LogInformation("Adding justification_state column to idle_periods");
            await connection.ExecuteAsync("ALTER TABLE idle_periods ADD COLUMN justification_state TEXT NOT NULL DEFAULT 'none'");
        }

        var idleJustificationReasonCodeExists = await connection.QueryFirstOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM pragma_table_info('idle_periods') WHERE name = 'justification_reason_code'");

        if (idleJustificationReasonCodeExists == 0)
        {
            _logger.LogInformation("Adding justification_reason_code column to idle_periods");
            await connection.ExecuteAsync("ALTER TABLE idle_periods ADD COLUMN justification_reason_code TEXT");
        }

        var idleJustificationNoteExists = await connection.QueryFirstOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM pragma_table_info('idle_periods') WHERE name = 'justification_note'");

        if (idleJustificationNoteExists == 0)
        {
            _logger.LogInformation("Adding justification_note column to idle_periods");
            await connection.ExecuteAsync("ALTER TABLE idle_periods ADD COLUMN justification_note TEXT");
        }

        var idleJustificationSubmittedAtExists = await connection.QueryFirstOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM pragma_table_info('idle_periods') WHERE name = 'justification_submitted_at_utc'");

        if (idleJustificationSubmittedAtExists == 0)
        {
            _logger.LogInformation("Adding justification_submitted_at_utc column to idle_periods");
            await connection.ExecuteAsync("ALTER TABLE idle_periods ADD COLUMN justification_submitted_at_utc TEXT");
        }

        var orgIdleJustificationPromptThresholdExists = await connection.QueryFirstOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM pragma_table_info('org_policies_cache') WHERE name = 'idle_justification_prompt_threshold_seconds'");

        if (orgIdleJustificationPromptThresholdExists == 0)
        {
            _logger.LogInformation("Adding idle_justification_prompt_threshold_seconds column to org_policies_cache");
            await connection.ExecuteAsync("ALTER TABLE org_policies_cache ADD COLUMN idle_justification_prompt_threshold_seconds INTEGER");
        }

        // Add work_goal_seconds column to local_settings
        var workGoalExists = await connection.QueryFirstOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM pragma_table_info('local_settings') WHERE name = 'work_goal_seconds'");

        if (workGoalExists == 0)
        {
            _logger.LogInformation("Adding work_goal_seconds column to local_settings");
            await connection.ExecuteAsync("ALTER TABLE local_settings ADD COLUMN work_goal_seconds INTEGER");
        }

        // Add devtools_enabled column to local_settings
        var devToolsEnabledExists = await connection.QueryFirstOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM pragma_table_info('local_settings') WHERE name = 'devtools_enabled'");

        if (devToolsEnabledExists == 0)
        {
            _logger.LogInformation("Adding devtools_enabled column to local_settings");
            await connection.ExecuteAsync("ALTER TABLE local_settings ADD COLUMN devtools_enabled INTEGER");
        }

        // Add devtools_enabled_until_utc column to local_settings
        var devToolsUntilExists = await connection.QueryFirstOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM pragma_table_info('local_settings') WHERE name = 'devtools_enabled_until_utc'");

        if (devToolsUntilExists == 0)
        {
            _logger.LogInformation("Adding devtools_enabled_until_utc column to local_settings");
            await connection.ExecuteAsync("ALTER TABLE local_settings ADD COLUMN devtools_enabled_until_utc TEXT");
        }

        // Add evidence policy columns to org_policies_cache
        await AddColumnIfNotExistsAsync(connection, "org_policies_cache", "screenshots_enabled", "INTEGER NOT NULL DEFAULT 0");
        await AddColumnIfNotExistsAsync(connection, "org_policies_cache", "screenshot_interval_minutes", "INTEGER NOT NULL DEFAULT 5");
        await AddColumnIfNotExistsAsync(connection, "org_policies_cache", "screenshot_excluded_apps_json", "TEXT NOT NULL DEFAULT '[]'");
        await AddColumnIfNotExistsAsync(connection, "org_policies_cache", "evidence_retention_days", "INTEGER NOT NULL DEFAULT 30");
        await AddColumnIfNotExistsAsync(connection, "org_policies_cache", "website_tracking_enabled", "INTEGER NOT NULL DEFAULT 1");
    }

    private async Task AddColumnIfNotExistsAsync(SqliteConnection connection, string table, string column, string definition)
    {
        var exists = await connection.QueryFirstOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM pragma_table_info(@Table) WHERE name = @Column",
            new { Table = table, Column = column });

        if (exists == 0)
        {
            _logger.LogInformation("Adding {Column} column to {Table}", column, table);
            await connection.ExecuteAsync($"ALTER TABLE {table} ADD COLUMN {column} {definition}");
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
            CREATE INDEX IF NOT EXISTS ix_activity_sessions_file_path ON activity_sessions(file_path);
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

            -- CX-143: App category cache indexes
            CREATE INDEX IF NOT EXISTS ix_app_category_cache_identifier ON app_category_cache(identifier);
            CREATE INDEX IF NOT EXISTS ix_app_category_cache_productivity ON app_category_cache(productivity);

            -- F2-E2: Evidence upload queue indexes
            CREATE INDEX IF NOT EXISTS ix_evidence_queue_status_next_attempt ON evidence_upload_queue(status, next_attempt_utc);
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
        await _lock.WaitAsync();
        try
        {
            await DisposeConnectionCoreAsync();
        }
        finally
        {
            _lock.Release();
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
