namespace TimeTrack.Agent.Contracts.Services;

/// <summary>
/// A point-in-time snapshot of the agent's health, passed to the heartbeat
/// so the backend always knows the agent's internal state.
/// </summary>
public sealed record AgentHealthSnapshot(
    string HealthStatus,           // healthy | degraded | unhealthy | unknown
    bool BackendReachable,
    int ConsecutiveSyncFailures,
    DateTime? LastSuccessfulSyncAt,
    bool IpcConnected
);
