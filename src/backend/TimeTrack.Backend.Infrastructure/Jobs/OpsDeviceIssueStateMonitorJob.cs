using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;
using TimeTrack.Backend.Infrastructure.Jobs.Interfaces;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Jobs;

public sealed class OpsDeviceIssueStateMonitorJob : IOpsDeviceIssueStateMonitorJob
{
    private readonly TimeTrackDbContext _db;
    private readonly IOpsDeviceIssueStateRepository _stateRepository;
    private readonly IPlatformEventLogRepository _platformEventLogRepository;
    private readonly IOpsMcpPushNotifier _pushNotifier;
    private readonly ILogger<OpsDeviceIssueStateMonitorJob> _logger;

    public OpsDeviceIssueStateMonitorJob(
        TimeTrackDbContext db,
        IOpsDeviceIssueStateRepository stateRepository,
        IPlatformEventLogRepository platformEventLogRepository,
        IOpsMcpPushNotifier pushNotifier,
        ILogger<OpsDeviceIssueStateMonitorJob> logger)
    {
        _db = db;
        _stateRepository = stateRepository;
        _platformEventLogRepository = platformEventLogRepository;
        _pushNotifier = pushNotifier;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        var now = DateTime.UtcNow;
        _logger.LogInformation("Ops device issue monitor tick at {Now}", now);

        // Active devices only; same basis as Maintenance.
        var devices = await _db.Devices
            .AsNoTracking()
            .Include(d => d.User)
            .Where(d => d.Status == DeviceStatus.Active)
            .ToListAsync();

        var pushed = false;

        foreach (var d in devices)
        {
            var lastSeen = d.LastHeartbeatAt ?? d.ActivatedAt;
            var isOffline = (now - lastSeen).TotalMinutes > 10;

            var issue = "none";
            var isActive = false;

            if (isOffline)
            {
                issue = "offline";
                isActive = true;
            }
            else if (d.HealthStatus is "unhealthy" or "degraded")
            {
                issue = d.HealthStatus!;
                isActive = true;
            }

            var existing = await _stateRepository.GetByDeviceIdAsync(d.Id);
            var transitioned = false;

            if (existing is null)
            {
                existing = OpsDeviceIssueState.Create(d.Id, d.OrgId, issue, isActive);
                transitioned = isActive; // first time and critical -> treat as transition
                await _stateRepository.UpsertAsync(existing);
            }
            else
            {
                var wasActive = existing.IsActive;
                var oldIssue = existing.Issue;
                if (existing.TransitionTo(issue, isActive))
                {
                    await _stateRepository.UpsertAsync(existing);
                }

                transitioned = !wasActive && isActive;
                if (!transitioned && wasActive && isActive && !string.Equals(oldIssue, issue, StringComparison.OrdinalIgnoreCase))
                {
                    // Issue changed while staying critical (e.g., degraded -> unhealthy)
                    transitioned = true;
                }
            }

            if (transitioned && isActive)
            {
                existing.MarkNotified();
                await _stateRepository.UpsertAsync(existing);

                var metadata = JsonSerializer.Serialize(new
                {
                    orgId = d.OrgId,
                    deviceId = d.Id,
                    hostname = d.Hostname,
                    userDisplayName = d.User?.DisplayName,
                    issue,
                    lastHeartbeatAtUtc = d.LastHeartbeatAt,
                    healthStatus = d.HealthStatus,
                    ipcConnected = d.IpcConnected
                });

                var idempotencyKey = $"device_issue_transition:{d.Id}:{issue}:{now:O}";

                var evt = PlatformEventLog.Create(
                    Guid.NewGuid(),
                    eventType: "device.issue_transition",
                    severity: "critical",
                    message: $"Device '{d.Hostname}' became {issue}",
                    metadataJson: metadata,
                    timestampUtc: now,
                    idempotencyKey: idempotencyKey);

                await _platformEventLogRepository.AddIfNotExistsAsync(evt);

                pushed = true;
            }
        }

        if (pushed)
        {
            await _pushNotifier.NotifyResourceUpdatedAsync("ops://critical");
        }
    }
}

