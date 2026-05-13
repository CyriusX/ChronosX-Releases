using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.AI.Services;

public sealed class AiDecisionLogger
{
    private readonly IAiDecisionLogRepository _repo;
    private readonly ILogger<AiDecisionLogger> _logger;

    public AiDecisionLogger(
        IAiDecisionLogRepository repo,
        ILogger<AiDecisionLogger> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task LogAsync(
        Guid orgId,
        Guid? userId,
        string decisionType,
        JsonElement input,
        JsonElement output,
        string modelVersion,
        double? confidence,
        int? tokensUsed,
        long latencyMs,
        CancellationToken ct)
    {
        try
        {
            var log = Domain.Entities.AiDecisionLog.Create(
                orgId, decisionType, input, output, modelVersion,
                userId, confidence, tokensUsed, (int)latencyMs);

            await _repo.AddAsync(log, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log AI decision for {DecisionType}", decisionType);
        }
    }
}
