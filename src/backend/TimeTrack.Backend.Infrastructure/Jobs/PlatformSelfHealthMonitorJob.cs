using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Jobs.Interfaces;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Jobs;

public sealed class PlatformSelfHealthMonitorJob : IPlatformSelfHealthMonitorJob
{
    private readonly TimeTrackDbContext _db;
    private readonly IPlatformHealthStateRepository _stateRepository;
    private readonly IPlatformEventLogRepository _eventLogRepository;
    private readonly IOpsMcpPushNotifier _pushNotifier;
    private readonly ILogger<PlatformSelfHealthMonitorJob> _logger;

    public PlatformSelfHealthMonitorJob(
        TimeTrackDbContext db,
        IPlatformHealthStateRepository stateRepository,
        IPlatformEventLogRepository eventLogRepository,
        IOpsMcpPushNotifier pushNotifier,
        ILogger<PlatformSelfHealthMonitorJob> logger)
    {
        _db = db;
        _stateRepository = stateRepository;
        _eventLogRepository = eventLogRepository;
        _pushNotifier = pushNotifier;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        var now = DateTime.UtcNow;

        var checks = new Dictionary<string, string>();
        string status;

        try
        {
            // Trivial DB check. If DB is unreachable, EF will throw.
            await _db.Database.ExecuteSqlRawAsync("SELECT 1;");
            checks["db"] = "healthy";
            status = "healthy";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Platform self health check failed");
            checks["db"] = "unhealthy";
            status = "unhealthy";
        }

        var checksJson = JsonSerializer.Serialize(checks);
        var state = await _stateRepository.GetAsync();

        var changed = state.Set(status, checksJson);
        await _stateRepository.UpdateAsync(state);

        if (!changed) return;

        var idempotencyKey = $"platform_health_transition:{status}:{state.LastChangedAtUtc:O}";
        var evt = PlatformEventLog.Create(
            Guid.NewGuid(),
            eventType: "platform.health_transition",
            severity: "critical",
            message: $"Platform health changed to {status}",
            metadataJson: checksJson,
            timestampUtc: now,
            idempotencyKey: idempotencyKey);

        await _eventLogRepository.AddIfNotExistsAsync(evt);

        await _pushNotifier.NotifyResourceUpdatedAsync("ops://platform-health");
        await _pushNotifier.NotifyResourceUpdatedAsync("ops://critical");
    }
}

