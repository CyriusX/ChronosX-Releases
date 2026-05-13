using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimeTrack.Api.Security;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Api.Controllers;

[ApiController]
[Route("api/v1/internal/ai-metrics")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class AiMetricsController : ControllerBase
{
    private readonly TimeTrackDbContext _context;
    private readonly ICurrentUserContext _currentUser;

    public AiMetricsController(
        TimeTrackDbContext context,
        ICurrentUserContext currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> GetMetrics(CancellationToken ct = default)
    {
        if (_currentUser.OrgId == null)
            return Forbid();

        var orgId = _currentUser.OrgId.Value;
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        var today = DateTime.UtcNow.Date;

        // Classification metrics
        var classificationDecisions = await _context.AiDecisionLogs
            .IgnoreQueryFilters()
            .Where(d => d.OrgId == orgId
                && d.DecisionType == "app_classification"
                && d.CreatedAt >= thirtyDaysAgo)
            .Select(d => new { d.WasReviewed, d.ReviewOutcome })
            .ToListAsync(ct);

        var totalClassifications = classificationDecisions.Count;
        var accepted = classificationDecisions.Count(d => d.ReviewOutcome == "accepted");
        var corrected = classificationDecisions.Count(d => d.ReviewOutcome == "corrected");
        var rejected = classificationDecisions.Count(d => d.ReviewOutcome == "rejected");
        var accuracyRate = totalClassifications > 0
            ? Math.Round((double)(accepted + corrected) / totalClassifications, 2)
            : 0;

        // Narrative metrics
        var narrativeDecisions = await _context.AiDecisionLogs
            .IgnoreQueryFilters()
            .Where(d => d.OrgId == orgId
                && d.DecisionType == "weekly_narrative"
                && d.CreatedAt >= thirtyDaysAgo)
            .Select(d => new { d.WasReviewed, d.ReviewOutcome })
            .ToListAsync(ct);

        var totalNarratives = narrativeDecisions.Count;
        var thumbsUp = narrativeDecisions.Count(d => d.ReviewOutcome == "useful");
        var thumbsDown = narrativeDecisions.Count(d => d.ReviewOutcome == "not_useful");
        var usefulRate = totalNarratives > 0
            ? Math.Round((double)thumbsUp / totalNarratives, 2)
            : 0;

        // Alert metrics
        var alertStats = await _context.SmartAlerts
            .IgnoreQueryFilters()
            .Where(a => a.OrgId == orgId && a.CreatedAt >= thirtyDaysAgo)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Read = g.Count(a => a.WasRead),
                Acted = g.Count(a => a.WasActed),
            })
            .FirstOrDefaultAsync(ct);

        var totalAlerts = alertStats?.Total ?? 0;
        var actedRate = totalAlerts > 0
            ? Math.Round((double)(alertStats?.Acted ?? 0) / totalAlerts, 2)
            : 0;

        // Cost metrics
        var costMetrics = await _context.AiDecisionLogs
            .IgnoreQueryFilters()
            .Where(d => d.OrgId == orgId && d.CreatedAt >= thirtyDaysAgo)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalTokens = g.Sum(d => (int?)d.TokensUsed) ?? 0,
                AvgLatencyMs = g.Any(d => d.LatencyMs != null)
                    ? (int)g.Where(d => d.LatencyMs != null).Average(d => d.LatencyMs!.Value)
                    : 0,
            })
            .FirstOrDefaultAsync(ct);

        var callsToday = await _context.AiDecisionLogs
            .IgnoreQueryFilters()
            .CountAsync(d => d.OrgId == orgId && d.CreatedAt.Date == today, ct);

        return Ok(new
        {
            classification = new
            {
                total_suggestions = totalClassifications,
                accepted,
                corrected,
                rejected,
                accuracy_rate = accuracyRate,
            },
            narratives = new
            {
                total_generated = totalNarratives,
                thumbs_up = thumbsUp,
                thumbs_down = thumbsDown,
                useful_rate = usefulRate,
            },
            alerts = new
            {
                total_generated = totalAlerts,
                read = alertStats?.Read ?? 0,
                acted = alertStats?.Acted ?? 0,
                acted_rate = actedRate,
            },
            costs = new
            {
                total_tokens_used_month = costMetrics?.TotalTokens ?? 0,
                avg_latency_ms = costMetrics?.AvgLatencyMs ?? 0,
                zai_calls_today = callsToday,
            },
        });
    }
}
