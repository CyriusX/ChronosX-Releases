using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Repositories;

/// <summary>
/// Implementação de consultas de relatórios otimizadas com GROUP BY
/// </summary>
public sealed class ReportRepository : IReportRepository
{
    private readonly TimeTrackDbContext _context;

    public ReportRepository(TimeTrackDbContext context)
    {
        _context = context;
    }

    public async Task<DailyActivityAggregate> GetDailyActivityAggregateAsync(
        Guid userId,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        // Normalizar data para o início e fim do dia em UTC
        var startOfDay = date.Date;
        var endOfDay = startOfDay.AddDays(1).AddTicks(-1);

        // Query otimizada com GROUP BY no banco - apps agregados
        var appAggregates = await _context.ActivitySessions
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.StartedAt >= startOfDay && a.StartedAt <= endOfDay)
            .GroupBy(a => a.ProcessName)
            .Select(g => new AppAggregate
            {
                ProcessName = g.Key,
                TotalSeconds = (long)g.Sum(a => a.DurationSeconds),
                SessionCount = g.Count()
            })
            .OrderByDescending(a => a.TotalSeconds)
            .ToListAsync(cancellationToken);

        // Query para primeira e última atividade
        var timeBounds = await _context.ActivitySessions
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.StartedAt >= startOfDay && a.StartedAt <= endOfDay)
            .GroupBy(_ => true)
            .Select(g => new
            {
                FirstActivity = g.Min(a => a.StartedAt),
                LastActivity = g.Max(a => a.EndedAt)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new DailyActivityAggregate
        {
            TotalSeconds = appAggregates.Sum(a => a.TotalSeconds),
            FirstActivity = timeBounds?.FirstActivity,
            LastActivity = timeBounds?.LastActivity,
            Apps = appAggregates
        };
    }

    public async Task<long> GetDailyIdleSecondsAsync(
        Guid userId,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        var startOfDay = date.Date;
        var endOfDay = startOfDay.AddDays(1).AddTicks(-1);

        return await _context.IdlePeriods
            .AsNoTracking()
            .Where(i => i.UserId == userId && i.StartedAt >= startOfDay && i.StartedAt <= endOfDay)
            .SumAsync(i => (long)i.DurationSeconds, cancellationToken);
    }

    public async Task<IEnumerable<AppAggregate>> GetTopAppsAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        int limit,
        CancellationToken cancellationToken = default)
    {
        // Normalizar datas
        var start = startDate.Date;
        var end = endDate.Date.AddDays(1).AddTicks(-1);

        return await _context.ActivitySessions
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.StartedAt >= start && a.StartedAt <= end)
            .GroupBy(a => a.ProcessName)
            .Select(g => new AppAggregate
            {
                ProcessName = g.Key,
                TotalSeconds = (long)g.Sum(a => a.DurationSeconds),
                SessionCount = g.Count()
            })
            .OrderByDescending(a => a.TotalSeconds)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}
