namespace TimeTrack.Backend.Application.Maintenance.DTOs;

// === Admin-side DTOs ===

public sealed class CreateRemoteCommandRequest
{
    public required string CommandType { get; init; }
    public object? Payload { get; init; }
}

public sealed class CreateRemoteCommandResponse
{
    public Guid CommandId { get; init; }
    public string Status { get; init; } = "pending";
}

public sealed class CommandHistoryResponse
{
    public IReadOnlyList<CommandHistoryItem> Commands { get; init; } = Array.Empty<CommandHistoryItem>();
}

public sealed class CommandHistoryItem
{
    public Guid Id { get; init; }
    public string CommandType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? PayloadJson { get; init; }
    public string? ResultJson { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? AcknowledgedAt { get; init; }
}

// === Agent-side DTOs ===

public sealed class PendingCommandsResponse
{
    public IReadOnlyList<PendingCommandItem> Commands { get; init; } = Array.Empty<PendingCommandItem>();
}

public sealed class PendingCommandItem
{
    public Guid Id { get; init; }
    public string CommandType { get; init; } = string.Empty;
    public string? PayloadJson { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class AcknowledgeCommandRequest
{
    public required string Status { get; init; }
    public string? ResultJson { get; init; }
}

// === Device Info ===

public sealed class DeviceInfoResponse
{
    public Guid DeviceId { get; init; }
    public string Hostname { get; init; } = string.Empty;
    public string? DeviceName { get; init; }
    public string AgentVersion { get; init; } = string.Empty;
    public string? OsVersion { get; init; }
    public string? IpAddress { get; init; }
    public int? UptimeSeconds { get; init; }
    public string? TrackingState { get; init; }
    public string? HealthStatus { get; init; }
    public int? ConsecutiveSyncFailures { get; init; }
    public DateTime? LastSuccessfulSyncAt { get; init; }
    public bool? IpcConnected { get; init; }
    public DateTime? LastHeartbeatAt { get; init; }
    public DateTime ActivatedAt { get; init; }
    public string Status { get; init; } = string.Empty;
    public string DisplayMode { get; init; } = string.Empty;
    public string? UserDisplayName { get; init; }
}

public sealed class HealthSummaryResponse
{
    public int TotalDevices { get; init; }
    public int OnlineCount { get; init; }
    public int OfflineCount { get; init; }
    public int DegradedCount { get; init; }
    public int UnhealthyCount { get; init; }
    public IReadOnlyList<HealthAlertItem> Alerts { get; init; } = Array.Empty<HealthAlertItem>();
}

public sealed class HealthAlertItem
{
    public Guid DeviceId { get; init; }
    public string Hostname { get; init; } = string.Empty;
    public string? UserDisplayName { get; init; }
    public string Issue { get; init; } = string.Empty;  // offline | degraded | unhealthy
    public DateTime? LastSeenAt { get; init; }
    public string? HealthStatus { get; init; }
}
