using System.ComponentModel;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;
using TimeTrack.Backend.Domain.ValueObjects;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Api.OpsMcp;

[McpServerResourceType]
public sealed class OpsMcpResources
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly TimeTrackDbContext _db;
    private readonly McpPlatformRequestContext _requestContext;
    private readonly IOpsMcpAuditLogger _audit;

    public OpsMcpResources(
        TimeTrackDbContext db,
        McpPlatformRequestContext requestContext,
        IOpsMcpAuditLogger audit)
    {
        _db = db;
        _requestContext = requestContext;
        _audit = audit;
    }

    [McpServerResource(
        Name = "ops_alerts",
        Title = "Ops Alerts",
        MimeType = "application/json",
        UriTemplate = "ops://alerts")]
    [Description("Latest device health alerts (offline/unhealthy/degraded).")]
    public async Task<string> AlertsAsync(CancellationToken cancellationToken = default)
    {
        var platformApiKeyId = _requestContext.PlatformApiKeyId ?? Guid.Empty;
        _audit.LogToolCall(platformApiKeyId, "resource:ops://alerts", null);

        var now = DateTime.UtcNow;

        var devices = await _db.Devices
            .AsNoTracking()
            .Include(d => d.User)
            .Where(d => d.Status == DeviceStatus.Active)
            .ToListAsync(cancellationToken);

        var alerts = new List<object>();
        foreach (var d in devices)
        {
            var lastSeen = d.LastHeartbeatAt ?? d.ActivatedAt;
            var isOffline = (now - lastSeen).TotalMinutes > 10;

            if (isOffline)
            {
                alerts.Add(new
                {
                    d.OrgId,
                    deviceId = d.Id,
                    d.Hostname,
                    userDisplayName = d.User?.DisplayName,
                    issue = "offline",
                    lastSeenAtUtc = d.LastHeartbeatAt,
                    healthStatus = "offline"
                });
                continue;
            }

            var health = d.HealthStatus;
            if (health is "unhealthy" or "degraded")
            {
                alerts.Add(new
                {
                    d.OrgId,
                    deviceId = d.Id,
                    d.Hostname,
                    userDisplayName = d.User?.DisplayName,
                    issue = health,
                    lastSeenAtUtc = d.LastHeartbeatAt,
                    healthStatus = health
                });
            }
        }

        var payload = new
        {
            generatedAtUtc = DateTime.UtcNow,
            count = alerts.Count,
            alerts
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    [McpServerResource(
        Name = "ops_events",
        Title = "Ops Events Feed",
        MimeType = "application/json",
        UriTemplate = "ops://events{?severity,sinceUtc,orgId,deviceId,category,limit}")]
    [Description("Recent events feed (agent_event_logs). Query parameters are optional.")]
    public async Task<string> EventsAsync(
        string? severity = null,
        DateTime? sinceUtc = null,
        Guid? orgId = null,
        Guid? deviceId = null,
        string? category = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveLimit = Math.Clamp(limit ?? 100, 1, 1000);

        var platformApiKeyId = _requestContext.PlatformApiKeyId ?? Guid.Empty;
        _audit.LogToolCall(platformApiKeyId, "resource:ops://events", new
        {
            severity,
            sinceUtc,
            orgId,
            deviceId,
            category,
            limit = effectiveLimit
        });

        var query = _db.AgentEventLogs.AsNoTracking();

        if (orgId.HasValue)
            query = query.Where(e => e.OrgId == orgId.Value);
        if (deviceId.HasValue)
            query = query.Where(e => e.DeviceId == deviceId.Value);
        if (sinceUtc.HasValue)
            query = query.Where(e => e.TimestampUtc >= sinceUtc.Value);
        if (!string.IsNullOrWhiteSpace(severity))
            query = query.Where(e => e.Severity == severity);
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(e => e.Category == category);

        var rows = await query
            .OrderByDescending(e => e.TimestampUtc)
            .Take(effectiveLimit)
            .ToListAsync(cancellationToken);

        var payload = new
        {
            generatedAtUtc = DateTime.UtcNow,
            count = rows.Count,
            items = rows.Select(e => new
            {
                e.Id,
                e.OrgId,
                e.DeviceId,
                e.EventType,
                e.Category,
                e.Severity,
                e.Message,
                metadata = OpsMcpMetadataRedactor.Redact(e.MetadataJson),
                e.TimestampUtc
            })
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    [McpServerResource(
        Name = "ops_device",
        Title = "Ops Device Snapshot",
        MimeType = "application/json",
        UriTemplate = "ops://device/{deviceId}{?orgId}")]
    [Description("Device info snapshot by deviceId (and optional orgId for disambiguation).")]
    public async Task<string> DeviceAsync(
        Guid deviceId,
        Guid? orgId = null,
        CancellationToken cancellationToken = default)
    {
        var platformApiKeyId = _requestContext.PlatformApiKeyId ?? Guid.Empty;
        _audit.LogToolCall(platformApiKeyId, "resource:ops://device", new { deviceId, orgId });

        var query = _db.Devices.AsNoTracking().Where(d => d.Id == deviceId);
        if (orgId.HasValue)
            query = query.Where(d => d.OrgId == orgId.Value);

        var device = await query.FirstOrDefaultAsync(cancellationToken);

        var payload = new
        {
            generatedAtUtc = DateTime.UtcNow,
            device = device is null ? null : new
            {
                device.OrgId,
                device.Id,
                device.UserId,
                device.Hostname,
                device.DeviceName,
                device.AgentVersion,
                device.OsVersion,
                device.IpAddress,
                device.UptimeSeconds,
                device.TrackingState,
                device.HealthStatus,
                device.ConsecutiveSyncFailures,
                lastSuccessfulSyncAtUtc = device.LastSuccessfulSyncAt,
                device.IpcConnected,
                activatedAtUtc = device.ActivatedAt,
                lastHeartbeatAtUtc = device.LastHeartbeatAt,
                status = device.Status.ToString()
            }
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    [McpServerResource(
        Name = "ops_critical",
        Title = "Ops Critical Snapshot",
        MimeType = "application/json",
        UriTemplate = "ops://critical")]
    [Description("Combined critical view: latest critical agent events + current critical device alerts + platform health snapshot.")]
    public async Task<string> CriticalAsync(CancellationToken cancellationToken = default)
    {
        var platformApiKeyId = _requestContext.PlatformApiKeyId ?? Guid.Empty;
        _audit.LogToolCall(platformApiKeyId, "resource:ops://critical", null);

        var now = DateTime.UtcNow;

        var criticalEvents = await _db.AgentEventLogs
            .AsNoTracking()
            .Where(e => e.Severity == "critical")
            .OrderByDescending(e => e.TimestampUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        var devices = await _db.Devices
            .AsNoTracking()
            .Include(d => d.User)
            .Where(d => d.Status == DeviceStatus.Active)
            .ToListAsync(cancellationToken);

        var alerts = new List<object>();
        foreach (var d in devices)
        {
            var lastSeen = d.LastHeartbeatAt ?? d.ActivatedAt;
            var isOffline = (now - lastSeen).TotalMinutes > 10;

            if (isOffline)
            {
                alerts.Add(new
                {
                    d.OrgId,
                    deviceId = d.Id,
                    d.Hostname,
                    userDisplayName = d.User?.DisplayName,
                    issue = "offline",
                    lastSeenAtUtc = d.LastHeartbeatAt,
                    healthStatus = "offline"
                });
                continue;
            }

            var health = d.HealthStatus;
            if (health is "unhealthy" or "degraded")
            {
                alerts.Add(new
                {
                    d.OrgId,
                    deviceId = d.Id,
                    d.Hostname,
                    userDisplayName = d.User?.DisplayName,
                    issue = health,
                    lastSeenAtUtc = d.LastHeartbeatAt,
                    healthStatus = health
                });
            }
        }

        var platformHealth = await _db.PlatformHealthState
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        var payload = new
        {
            generatedAtUtc = now,
            platformHealth = platformHealth is null ? null : new
            {
                platformHealth.Status,
                platformHealth.ChecksJson,
                platformHealth.LastChangedAtUtc,
                platformHealth.UpdatedAtUtc
            },
            criticalAlerts = alerts,
            criticalEvents = criticalEvents.Select(e => new
            {
                e.Id,
                e.OrgId,
                e.DeviceId,
                e.EventType,
                e.Category,
                e.Severity,
                e.Message,
                metadata = OpsMcpMetadataRedactor.Redact(e.MetadataJson),
                e.TimestampUtc
            })
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    [McpServerResource(
        Name = "ops_platform_health",
        Title = "Ops Platform Health",
        MimeType = "application/json",
        UriTemplate = "ops://platform-health")]
    [Description("Platform health snapshot derived from in-app self monitoring.")]
    public async Task<string> PlatformHealthAsync(CancellationToken cancellationToken = default)
    {
        var platformApiKeyId = _requestContext.PlatformApiKeyId ?? Guid.Empty;
        _audit.LogToolCall(platformApiKeyId, "resource:ops://platform-health", null);

        var state = await _db.PlatformHealthState
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        var payload = new
        {
            generatedAtUtc = DateTime.UtcNow,
            status = state?.Status ?? "unknown",
            checksJson = state?.ChecksJson ?? "{}",
            lastChangedAtUtc = state?.LastChangedAtUtc,
            updatedAtUtc = state?.UpdatedAtUtc
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }
}
