using TimeTrack.Agent.Contracts.Configuration;

namespace TimeTrack.AgentService.Configuration;

/// <summary>
/// Configurações do Agent Service
/// </summary>
public sealed class AgentSettings
{
    /// <summary>
    /// Seção de configuração
    /// </summary>
    public const string SectionName = "Agent";

    /// <summary>
    /// Intervalo de polling em milissegundos
    /// </summary>
    public int PollingIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Limiar de inatividade em segundos
    /// </summary>
    public int IdleThresholdSeconds { get; set; } = 60;

    /// <summary>
    /// Timeout para graceful shutdown em segundos
    /// </summary>
    public int GracefulShutdownTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Prioridade do processo
    /// </summary>
    public string ProcessPriority { get; set; } = "BelowNormal";

    /// <summary>
    /// Caminho do banco de dados SQLite local
    /// </summary>
    public string DatabasePath { get; set; } = "timetrack.db";

    /// <summary>
    /// Habilita modo de diagnóstico
    /// </summary>
    public bool EnableDiagnostics { get; set; } = false;

    /// <summary>
    /// Configurações de sincronização
    /// </summary>
    public SyncSettings Sync { get; set; } = new();
}
