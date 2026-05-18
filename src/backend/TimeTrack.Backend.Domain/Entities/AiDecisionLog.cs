using System.Text.Json;

namespace TimeTrack.Backend.Domain.Entities;

public sealed class AiDecisionLog
{
    public Guid Id { get; private set; }
    public Guid? UserId { get; private set; }
    public Guid OrgId { get; private set; }
    public string DecisionType { get; private set; } = string.Empty;
    public JsonElement InputData { get; private set; }
    public JsonElement Output { get; private set; }
    public string ModelVersion { get; private set; } = string.Empty;
    public double? Confidence { get; private set; }
    public int? TokensUsed { get; private set; }
    public int? LatencyMs { get; private set; }
    public bool WasReviewed { get; private set; }
    public string? ReviewOutcome { get; private set; }
    public JsonElement? CorrectValue { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private AiDecisionLog() { }

    public static AiDecisionLog Create(
        Guid orgId,
        string decisionType,
        JsonElement inputData,
        JsonElement output,
        string modelVersion,
        Guid? userId = null,
        double? confidence = null,
        int? tokensUsed = null,
        int? latencyMs = null)
    {
        return new AiDecisionLog
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            UserId = userId,
            DecisionType = decisionType,
            InputData = inputData,
            Output = output,
            ModelVersion = modelVersion,
            Confidence = confidence,
            TokensUsed = tokensUsed,
            LatencyMs = latencyMs,
            WasReviewed = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void MarkReviewed(string outcome, JsonElement? correctValue = null)
    {
        WasReviewed = true;
        ReviewOutcome = outcome;
        CorrectValue = correctValue;
    }
}
