using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Repositories;

public sealed class AiDecisionLogRepository : IAiDecisionLogRepository
{
    private readonly TimeTrackDbContext _context;

    public AiDecisionLogRepository(TimeTrackDbContext context)
    {
        _context = context;
    }

    public async Task<AiDecisionLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.AiDecisionLogs.FindAsync([id], cancellationToken);
    }

    public async Task<IEnumerable<AiDecisionLog>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.AiDecisionLogs.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(AiDecisionLog entity, CancellationToken cancellationToken = default)
    {
        await _context.AiDecisionLogs.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(AiDecisionLog entity, CancellationToken cancellationToken = default)
    {
        _context.AiDecisionLogs.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(AiDecisionLog entity, CancellationToken cancellationToken = default)
    {
        _context.AiDecisionLogs.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<AiDecisionLog>> GetPendingByOrgAsync(Guid orgId, string? decisionType = null, CancellationToken ct = default)
    {
        var query = _context.AiDecisionLogs
            .IgnoreQueryFilters()
            .Where(a => a.OrgId == orgId && !a.WasReviewed);

        if (decisionType is not null)
            query = query.Where(a => a.DecisionType == decisionType);

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);
    }

    async Task<AiDecisionLog?> IAiDecisionLogRepository.GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await _context.AiDecisionLogs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task<(int Total, int Accepted, int Corrected, int Rejected)> GetClassificationStatsAsync(Guid orgId, CancellationToken ct = default)
    {
        var logs = await _context.AiDecisionLogs
            .IgnoreQueryFilters()
            .Where(a => a.OrgId == orgId && a.DecisionType == "app_classification")
            .Select(a => a.ReviewOutcome)
            .ToListAsync(ct);

        return (
            logs.Count,
            logs.Count(r => r == "accepted"),
            logs.Count(r => r == "corrected"),
            logs.Count(r => r == "rejected")
        );
    }

    public async Task<(int Total, int ThumbsUp, int ThumbsDown)> GetNarrativeFeedbackStatsAsync(Guid orgId, CancellationToken ct = default)
    {
        var logs = await _context.AiDecisionLogs
            .IgnoreQueryFilters()
            .Where(a => a.OrgId == orgId && a.DecisionType == "weekly_narrative" && a.WasReviewed)
            .Select(a => a.ReviewOutcome)
            .ToListAsync(ct);

        return (
            logs.Count,
            logs.Count(r => r == "useful"),
            logs.Count(r => r == "not_useful")
        );
    }

    public async Task<(long TotalTokensMonth, double AvgLatencyMs, int CallsToday)> GetCostStatsAsync(Guid orgId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var todayStart = now.Date;

        var monthLogs = await _context.AiDecisionLogs
            .IgnoreQueryFilters()
            .Where(a => a.OrgId == orgId && a.CreatedAt >= monthStart && a.TokensUsed != null)
            .Select(a => new { a.TokensUsed, a.LatencyMs })
            .ToListAsync(ct);

        var todayCalls = await _context.AiDecisionLogs
            .IgnoreQueryFilters()
            .Where(a => a.OrgId == orgId && a.CreatedAt >= todayStart)
            .CountAsync(ct);

        var totalTokens = monthLogs.Sum(l => l.TokensUsed ?? 0);
        var avgLatency = monthLogs.Count > 0
            ? monthLogs.Average(l => l.LatencyMs ?? 0)
            : 0;

        return (totalTokens, avgLatency, todayCalls);
    }
}
