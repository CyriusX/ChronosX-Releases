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

        var createTablesSql = @"
            -- Tabela de estado do tracking
            CREATE TABLE IF NOT EXISTS tracking_state (
                id TEXT PRIMARY KEY,
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
                start_utc TEXT NOT NULL,
                end_utc TEXT NOT NULL,
                threshold_seconds INTEGER NOT NULL,
                is_system_detected INTEGER NOT NULL DEFAULT 1,
                created_at TEXT NOT NULL DEFAULT (datetime('now'))
            );

            -- Índices para consultas comuns
            CREATE INDEX IF NOT EXISTS ix_activity_sessions_start_utc ON activity_sessions(start_utc);
            CREATE INDEX IF NOT EXISTS ix_activity_sessions_exe_path_hash ON activity_sessions(exe_path_hash);
            CREATE INDEX IF NOT EXISTS ix_idle_periods_start_utc ON idle_periods(start_utc);
        ";

        await connection.ExecuteAsync(createTablesSql);

        _initialized = true;
        _logger.LogInformation("SQLite schema initialized successfully");
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
