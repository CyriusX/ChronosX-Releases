using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Domain.Entities;

/// <summary>
/// Representa um dispositivo registrado de um usuário
/// </summary>
public sealed class Device
{
    public Guid Id { get; private set; }
    public Guid OrgId { get; private set; }
    public Guid UserId { get; private set; }
    public string Hostname { get; private set; } = string.Empty;
    public string? DeviceName { get; private set; }
    public string AgentVersion { get; private set; } = string.Empty;
    public DisplayMode DisplayMode { get; private set; }
    public DeviceStatus Status { get; private set; }
    public DateTime ActivatedAt { get; private set; }
    public DateTime? LastHeartbeatAt { get; private set; }
    public string? OsVersion { get; private set; }
    public string? IpAddress { get; private set; }
    public int? UptimeSeconds { get; private set; }
    public string? TrackingState { get; private set; }
    public string? HealthStatus { get; private set; }
    public int? ConsecutiveSyncFailures { get; private set; }
    public DateTime? LastSuccessfulSyncAt { get; private set; }
    public bool? IpcConnected { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Navigation properties
    public User? User { get; private set; }

    private Device() { }

    public static Device Create(
        Guid id,
        Guid orgId,
        Guid userId,
        string hostname,
        string agentVersion,
        DisplayMode displayMode,
        string? deviceName = null)
    {
        if (string.IsNullOrWhiteSpace(hostname))
            throw new ArgumentException("Hostname is required", nameof(hostname));

        if (string.IsNullOrWhiteSpace(agentVersion))
            throw new ArgumentException("Agent version is required", nameof(agentVersion));

        return new Device
        {
            Id = id,
            OrgId = orgId,
            UserId = userId,
            Hostname = hostname,
            DeviceName = deviceName,
            AgentVersion = agentVersion,
            DisplayMode = displayMode,
            Status = DeviceStatus.Active,
            ActivatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void RecordHeartbeat(
        string? newAgentVersion = null,
        string? osVersion = null,
        string? ipAddress = null,
        int? uptimeSeconds = null,
        string? trackingState = null,
        string? healthStatus = null,
        bool? backendReachable = null,
        int? consecutiveSyncFailures = null,
        DateTime? lastSuccessfulSyncAt = null,
        bool? ipcConnected = null)
    {
        LastHeartbeatAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(newAgentVersion))
            AgentVersion = newAgentVersion;
        if (osVersion != null)
            OsVersion = osVersion;
        if (ipAddress != null)
            IpAddress = ipAddress;
        if (uptimeSeconds.HasValue)
            UptimeSeconds = uptimeSeconds;
        if (trackingState != null)
            TrackingState = trackingState;
        if (healthStatus != null)
            HealthStatus = healthStatus;
        if (consecutiveSyncFailures.HasValue)
            ConsecutiveSyncFailures = consecutiveSyncFailures;
        if (lastSuccessfulSyncAt.HasValue)
            LastSuccessfulSyncAt = lastSuccessfulSyncAt;
        if (ipcConnected.HasValue)
            IpcConnected = ipcConnected;
    }

    public void UpdateDisplayMode(DisplayMode newDisplayMode)
    {
        DisplayMode = newDisplayMode;
    }

    public void Deactivate()
    {
        Status = DeviceStatus.Inactive;
    }

    public void Reactivate()
    {
        Status = DeviceStatus.Active;
    }
}
