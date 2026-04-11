namespace TimeTrack.MacOSAgentService.Configuration;

public sealed class MacOSAgentSettings
{
    public const string SectionName = "Agent";

    public int PollingIntervalMs { get; set; } = 1000;
    public int IdleThresholdSeconds { get; set; } = 300;
    public int GracefulShutdownTimeoutSeconds { get; set; } = 10;
    public string DatabasePath { get; set; } = "timetrack.db";
    public bool EnableDiagnostics { get; set; } = false;
    public string? SocketPath { get; set; }
    public SyncSettings? Sync { get; set; }
}

public sealed class SyncSettings
{
    public string? BackendUrl { get; set; }
    public string? AuthToken { get; set; }
    public int SyncIntervalSeconds { get; set; } = 60;
    public int MaxBatchSize { get; set; } = 100;
    public long MaxBatchSizeBytes { get; set; } = 1048576;
    public int HttpTimeoutSeconds { get; set; } = 30;
    public int ConsecutiveFailuresAlertThreshold { get; set; } = 5;
}
