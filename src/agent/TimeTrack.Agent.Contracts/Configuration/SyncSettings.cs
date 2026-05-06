namespace TimeTrack.Agent.Contracts.Configuration;

/// <summary>
/// Configurações de sincronização com o backend
/// </summary>
public sealed class SyncSettings
{
    /// <summary>
    /// URL base do backend
    /// </summary>
    public string BackendUrl { get; set; } = "https://chronosx-dev-timetrack-api.gpoda0.easypanel.host";

    /// <summary>
    /// Token JWT para autenticação
    /// </summary>
    public string? AuthToken { get; set; }

    /// <summary>
    /// Intervalo entre ciclos de sync em segundos
    /// </summary>
    public int SyncIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Tamanho máximo do batch (número de itens)
    /// </summary>
    public int MaxBatchSize { get; set; } = 100;

    /// <summary>
    /// Tamanho máximo do batch em bytes (1MB default)
    /// </summary>
    public int MaxBatchSizeBytes { get; set; } = 1024 * 1024;

    /// <summary>
    /// Timeout HTTP em segundos
    /// </summary>
    public int HttpTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Número de falhas consecutivas para alertar
    /// </summary>
    public int ConsecutiveFailuresAlertThreshold { get; set; } = 5;
}
