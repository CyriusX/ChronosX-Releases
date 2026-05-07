using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Domain.ValueObjects;
using TimeTrack.Backend.Infrastructure.Jobs.Interfaces;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Jobs;

/// <summary>
/// Pauses open task timers when a user's devices have not heartbeated recently.
/// This avoids counting offline gaps (e.g., laptop asleep/off) as worked time.
/// </summary>
public sealed class TaskTimerStalePauseJob : ITaskTimerStalePauseJob
{
    private readonly TimeTrackDbContext _context;
    private readonly ILogger<TaskTimerStalePauseJob> _logger;

    private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(10);

    public TaskTimerStalePauseJob(TimeTrackDbContext context, ILogger<TaskTimerStalePauseJob> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        var now = DateTime.UtcNow;
        var cutoff = now - StaleThreshold;

        // Build (OrgId, UserId) => latest heartbeat map for users with at least one device heartbeat.
        var latestHeartbeats = await _context.Devices
            .AsNoTracking()
            .Where(d => d.Status == DeviceStatus.Active && d.LastHeartbeatAt != null)
            .GroupBy(d => new { d.OrgId, d.UserId })
            .Select(g => new
            {
                g.Key.OrgId,
                g.Key.UserId,
                LatestHeartbeatAt = g.Max(d => d.LastHeartbeatAt)!.Value
            })
            .Where(x => x.LatestHeartbeatAt < cutoff)
            .ToListAsync();

        if (latestHeartbeats.Count == 0)
            return;

        var heartbeatLookup = latestHeartbeats.ToDictionary(
            x => (x.OrgId, x.UserId),
            x => x.LatestHeartbeatAt);

        // Open + unpaused entries only. At most one open entry per user (DB invariant),
        // so this list is expected to be small.
        var openEntries = await _context.TaskTimeEntries
            .Where(e => e.EndedAt == null && e.PausedAt == null)
            .ToListAsync();

        var pausedCount = 0;
        foreach (var entry in openEntries)
        {
            if (!heartbeatLookup.TryGetValue((entry.OrgId, entry.UserId), out var lastHeartbeatAt))
                continue;

            // Pause at the last known heartbeat time (not 'now') so offline gap isn't counted.
            entry.PauseAt(lastHeartbeatAt);
            if (entry.IsPaused)
                pausedCount++;
        }

        if (pausedCount > 0)
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation(
                "TaskTimerStalePauseJob paused {Count} open task timers (cutoff={CutoffUtc:o})",
                pausedCount, cutoff);
        }
    }
}

