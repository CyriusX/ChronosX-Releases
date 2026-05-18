using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.ValueObjects;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Api.OpsMcp;

[McpServerToolType]
public sealed class OpsMcpTools
{
    private readonly TimeTrackDbContext _db;
    private readonly McpPlatformRequestContext _requestContext;
    private readonly IOpsMcpAuditLogger _audit;

    public OpsMcpTools(
        TimeTrackDbContext db,
        McpPlatformRequestContext requestContext,
        IOpsMcpAuditLogger audit)
    {
        _db = db;
        _requestContext = requestContext;
        _audit = audit;
    }

    public sealed class OpsHealthAlertItem
    {
        public Guid OrgId { get; init; }
        public Guid DeviceId { get; init; }
        public string Hostname { get; init; } = string.Empty;
        public string? UserDisplayName { get; init; }
        public string Issue { get; init; } = string.Empty; // offline|unhealthy|degraded
        public DateTime? LastSeenAtUtc { get; init; }
        public string HealthStatus { get; init; } = string.Empty;
    }

    public sealed class OpsListRecentAgentEventsArgs
    {
        public Guid? OrgId { get; init; }
        public Guid? DeviceId { get; init; }
        public DateTime? SinceUtc { get; init; }
        public string? Severity { get; init; }
        public string? Category { get; init; }
        public int? Limit { get; init; }
    }

    public sealed class OpsAgentEventItem
    {
        public Guid Id { get; init; }
        public Guid OrgId { get; init; }
        public Guid DeviceId { get; init; }
        public string EventType { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public string Severity { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public object? Metadata { get; init; }
        public DateTime TimestampUtc { get; init; }
    }

    public sealed class OpsDeviceInfo
    {
        public Guid OrgId { get; init; }
        public Guid DeviceId { get; init; }
        public Guid UserId { get; init; }
        public string Hostname { get; init; } = string.Empty;
        public string? DeviceName { get; init; }
        public string AgentVersion { get; init; } = string.Empty;
        public string? OsVersion { get; init; }
        public string? IpAddress { get; init; }
        public int? UptimeSeconds { get; init; }
        public string? TrackingState { get; init; }
        public string? HealthStatus { get; init; }
        public int? ConsecutiveSyncFailures { get; init; }
        public DateTime? LastSuccessfulSyncAtUtc { get; init; }
        public bool? IpcConnected { get; init; }
        public DateTime ActivatedAtUtc { get; init; }
        public DateTime? LastHeartbeatAtUtc { get; init; }
        public string Status { get; init; } = string.Empty;
    }

    [McpServerTool(
        Name = "ops_list_health_alerts",
        ReadOnly = true,
        OpenWorld = false,
        Destructive = false,
        Idempotent = true)]
    [Description("Lists offline/unhealthy/degraded devices (Maintenance-style) across all orgs or filtered by orgId.")]
    public async Task<IReadOnlyList<OpsHealthAlertItem>> OpsListHealthAlertsAsync(
        [Description("Optional orgId to filter results. Omit for all orgs.")] Guid? orgId = null,
        [Description("Optional issue filter: offline|unhealthy|degraded.")] string? issueFilter = null,
        [Description("Max results to return (1-1000). Default 200.")] int limit = 200,
        CancellationToken cancellationToken = default)
    {
        var platformApiKeyId = _requestContext.PlatformApiKeyId ?? Guid.Empty;
        _audit.LogToolCall(platformApiKeyId, "ops_list_health_alerts", new { orgId, issueFilter, limit });

        limit = Math.Clamp(limit, 1, 1000);
        var now = DateTime.UtcNow;

        var devicesQuery = _db.Devices
            .AsNoTracking()
            .Include(d => d.User)
            .Where(d => d.Status == DeviceStatus.Active);

        if (orgId.HasValue)
            devicesQuery = devicesQuery.Where(d => d.OrgId == orgId.Value);

        var devices = await devicesQuery.ToListAsync(cancellationToken);

        var alerts = new List<OpsHealthAlertItem>();
        foreach (var d in devices)
        {
            var lastSeen = d.LastHeartbeatAt ?? d.ActivatedAt;
            var isOffline = (now - lastSeen).TotalMinutes > 10;

            if (isOffline)
            {
                alerts.Add(new OpsHealthAlertItem
                {
                    OrgId = d.OrgId,
                    DeviceId = d.Id,
                    Hostname = d.Hostname,
                    UserDisplayName = d.User?.DisplayName,
                    Issue = "offline",
                    LastSeenAtUtc = d.LastHeartbeatAt,
                    HealthStatus = "offline"
                });
                continue;
            }

            var health = d.HealthStatus;
            if (health is "unhealthy" or "degraded")
            {
                alerts.Add(new OpsHealthAlertItem
                {
                    OrgId = d.OrgId,
                    DeviceId = d.Id,
                    Hostname = d.Hostname,
                    UserDisplayName = d.User?.DisplayName,
                    Issue = health,
                    LastSeenAtUtc = d.LastHeartbeatAt,
                    HealthStatus = health
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(issueFilter))
        {
            var normalized = issueFilter.Trim().ToLowerInvariant();
            alerts = alerts.Where(a => a.Issue.Equals(normalized, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return alerts
            .OrderByDescending(a => a.LastSeenAtUtc ?? DateTime.MinValue)
            .Take(limit)
            .ToList();
    }

    [McpServerTool(
        Name = "ops_list_recent_agent_events",
        ReadOnly = true,
        OpenWorld = false,
        Destructive = false,
        Idempotent = true)]
    [Description("Lists recent agent events (agent_event_logs) across all orgs or filtered by orgId/deviceId.")]
    public async Task<IReadOnlyList<OpsAgentEventItem>> OpsListRecentAgentEventsAsync(
        [Description("Query filters. Omitting orgId/deviceId searches across all orgs.")] OpsListRecentAgentEventsArgs args,
        CancellationToken cancellationToken = default)
    {
        var limit = Math.Clamp(args.Limit ?? 100, 1, 1000);
        var platformApiKeyId = _requestContext.PlatformApiKeyId ?? Guid.Empty;
        _audit.LogToolCall(platformApiKeyId, "ops_list_recent_agent_events", new
        {
            args.OrgId,
            args.DeviceId,
            args.SinceUtc,
            args.Severity,
            args.Category,
            limit
        });

        var query = _db.AgentEventLogs.AsNoTracking();

        if (args.OrgId.HasValue)
            query = query.Where(e => e.OrgId == args.OrgId.Value);

        if (args.DeviceId.HasValue)
            query = query.Where(e => e.DeviceId == args.DeviceId.Value);

        if (args.SinceUtc.HasValue)
            query = query.Where(e => e.TimestampUtc >= args.SinceUtc.Value);

        if (!string.IsNullOrWhiteSpace(args.Severity))
            query = query.Where(e => e.Severity == args.Severity);

        if (!string.IsNullOrWhiteSpace(args.Category))
            query = query.Where(e => e.Category == args.Category);

        var rows = await query
            .OrderByDescending(e => e.TimestampUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return rows.Select(e => new OpsAgentEventItem
        {
            Id = e.Id,
            OrgId = e.OrgId,
            DeviceId = e.DeviceId,
            EventType = e.EventType,
            Category = e.Category,
            Severity = e.Severity,
            Message = e.Message,
            Metadata = OpsMcpMetadataRedactor.Redact(e.MetadataJson),
            TimestampUtc = e.TimestampUtc
        }).ToList();
    }

    [McpServerTool(
        Name = "ops_get_device_info",
        ReadOnly = true,
        OpenWorld = false,
        Destructive = false,
        Idempotent = true)]
    [Description("Gets device info (OS, IP, uptime, tracking state, health) by orgId + deviceId.")]
    public async Task<OpsDeviceInfo?> OpsGetDeviceInfoAsync(
        [Description("OrgId owning the device.")] Guid orgId,
        [Description("DeviceId to fetch.")] Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        var platformApiKeyId = _requestContext.PlatformApiKeyId ?? Guid.Empty;
        _audit.LogToolCall(platformApiKeyId, "ops_get_device_info", new { orgId, deviceId });

        var device = await _db.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.OrgId == orgId && d.Id == deviceId, cancellationToken);

        if (device is null) return null;

        return new OpsDeviceInfo
        {
            OrgId = device.OrgId,
            DeviceId = device.Id,
            UserId = device.UserId,
            Hostname = device.Hostname,
            DeviceName = device.DeviceName,
            AgentVersion = device.AgentVersion,
            OsVersion = device.OsVersion,
            IpAddress = device.IpAddress,
            UptimeSeconds = device.UptimeSeconds,
            TrackingState = device.TrackingState,
            HealthStatus = device.HealthStatus,
            ConsecutiveSyncFailures = device.ConsecutiveSyncFailures,
            LastSuccessfulSyncAtUtc = device.LastSuccessfulSyncAt,
            IpcConnected = device.IpcConnected,
            ActivatedAtUtc = device.ActivatedAt,
            LastHeartbeatAtUtc = device.LastHeartbeatAt,
            Status = device.Status.ToString()
        };
    }

    [McpServerTool(
        Name = "ops_get_device_metrics",
        ReadOnly = true,
        OpenWorld = false,
        Destructive = false,
        Idempotent = true)]
    [Description("Gets recent machine metrics for a device (Maintenance-style).")]
    public async Task<object?> OpsGetDeviceMetricsAsync(
        [Description("OrgId owning the device.")] Guid orgId,
        [Description("DeviceId to fetch.")] Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        var platformApiKeyId = _requestContext.PlatformApiKeyId ?? Guid.Empty;
        _audit.LogToolCall(platformApiKeyId, "ops_get_device_metrics", new { orgId, deviceId });

        // NOTE: There's no existing shared DTO here that is safe for MCP. Return a compact shape.
        var metrics = await _db.MachineMetrics
            .AsNoTracking()
            .Where(m => m.OrgId == orgId && m.DeviceId == deviceId)
            .OrderByDescending(m => m.SampledAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        return new
        {
            orgId,
            deviceId,
            count = metrics.Count,
            items = metrics.Select(m => new
            {
                sampledAtUtc = m.SampledAtUtc,
                cpuPercent = m.CpuPercent,
                memoryUsedMb = m.MemoryUsedMb,
                memoryTotalMb = m.MemoryTotalMb,
                memoryPercent = m.MemoryTotalMb <= 0 ? 0 : (double)m.MemoryUsedMb / m.MemoryTotalMb * 100.0,
                diskUsedGb = m.DiskUsedGb,
                diskTotalGb = m.DiskTotalGb,
                diskPercent = m.DiskTotalGb <= 0 ? 0 : m.DiskUsedGb / m.DiskTotalGb * 100.0
            })
        };
    }

    [McpServerTool(
        Name = "ops_get_device_events",
        ReadOnly = true,
        OpenWorld = false,
        Destructive = false,
        Idempotent = true)]
    [Description("Gets recent agent events for a device (orgId + deviceId) with optional category/severity filters.")]
    public async Task<IReadOnlyList<OpsAgentEventItem>> OpsGetDeviceEventsAsync(
        [Description("OrgId owning the device.")] Guid orgId,
        [Description("DeviceId to fetch.")] Guid deviceId,
        [Description("Optional category filter.")] string? category = null,
        [Description("Optional severity filter.")] string? severity = null,
        [Description("Max results to return (1-1000). Default 100.")] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 1000);
        var platformApiKeyId = _requestContext.PlatformApiKeyId ?? Guid.Empty;
        _audit.LogToolCall(platformApiKeyId, "ops_get_device_events", new { orgId, deviceId, category, severity, limit });

        var query = _db.AgentEventLogs
            .AsNoTracking()
            .Where(e => e.OrgId == orgId && e.DeviceId == deviceId);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(e => e.Category == category);

        if (!string.IsNullOrWhiteSpace(severity))
            query = query.Where(e => e.Severity == severity);

        var rows = await query
            .OrderByDescending(e => e.TimestampUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return rows.Select(e => new OpsAgentEventItem
        {
            Id = e.Id,
            OrgId = e.OrgId,
            DeviceId = e.DeviceId,
            EventType = e.EventType,
            Category = e.Category,
            Severity = e.Severity,
            Message = e.Message,
            Metadata = OpsMcpMetadataRedactor.Redact(e.MetadataJson),
            TimestampUtc = e.TimestampUtc
        }).ToList();
    }
}
