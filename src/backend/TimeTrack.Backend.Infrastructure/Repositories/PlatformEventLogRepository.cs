using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Repositories;

public sealed class PlatformEventLogRepository : IPlatformEventLogRepository
{
    private readonly TimeTrackDbContext _context;

    public PlatformEventLogRepository(TimeTrackDbContext context)
    {
        _context = context;
    }

    public async Task AddIfNotExistsAsync(PlatformEventLog entity, CancellationToken cancellationToken = default)
    {
        // Use ON CONFLICT DO NOTHING to keep idempotency_key semantics.
        await _context.Database.ExecuteSqlRawAsync(@"
            INSERT INTO platform_event_logs
                (id, event_type, severity, message, metadata_json, timestamp_utc, idempotency_key, created_at_utc)
            VALUES
                ({0}, {1}, {2}, {3}, {4}, {5}, {6}, now())
            ON CONFLICT (idempotency_key) DO NOTHING;
        ",
            entity.Id,
            entity.EventType,
            entity.Severity,
            entity.Message,
            entity.MetadataJson,
            entity.TimestampUtc,
            entity.IdempotencyKey);
    }

    public async Task<IReadOnlyList<PlatformEventLog>> ListAsync(
        DateTime? sinceUtc,
        string? severity,
        int limit,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 500);

        var query = _context.PlatformEventLogs.AsNoTracking();

        if (sinceUtc.HasValue)
            query = query.Where(e => e.TimestampUtc >= sinceUtc.Value);

        if (!string.IsNullOrWhiteSpace(severity))
            query = query.Where(e => e.Severity == severity);

        return await query
            .OrderByDescending(e => e.TimestampUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}

