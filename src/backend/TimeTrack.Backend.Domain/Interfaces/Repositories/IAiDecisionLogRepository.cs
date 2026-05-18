using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Domain.Interfaces.Repositories;

public interface IAiDecisionLogRepository : IRepository<AiDecisionLog>
{
    Task<IEnumerable<AiDecisionLog>> GetPendingByOrgAsync(Guid orgId, string? decisionType = null, CancellationToken ct = default);
    new Task<AiDecisionLog?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<(int Total, int Accepted, int Corrected, int Rejected)> GetClassificationStatsAsync(Guid orgId, CancellationToken ct = default);
    Task<(int Total, int ThumbsUp, int ThumbsDown)> GetNarrativeFeedbackStatsAsync(Guid orgId, CancellationToken ct = default);
    Task<(long TotalTokensMonth, double AvgLatencyMs, int CallsToday)> GetCostStatsAsync(Guid orgId, CancellationToken ct = default);
}
