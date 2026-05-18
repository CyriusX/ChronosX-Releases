using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Application.Insights.DTOs;
using TimeTrack.Backend.Application.Insights.Services;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Api.Insights.Queries;

public sealed record GetDailyInsightQuery(string? Date, Guid UserId, Guid OrgId) : IRequest<DailyInsightResponse>;

public sealed class GetDailyInsightQueryHandler
    : IRequestHandler<GetDailyInsightQuery, DailyInsightResponse>
{
    private readonly TimeTrackDbContext _context;

    public GetDailyInsightQueryHandler(TimeTrackDbContext context)
    {
        _context = context;
    }

    public async Task<DailyInsightResponse> Handle(GetDailyInsightQuery request, CancellationToken ct)
    {
        var targetDate = !string.IsNullOrEmpty(request.Date) && DateOnly.TryParse(request.Date, CultureInfo.InvariantCulture, out var d)
            ? d
            : DateOnly.FromDateTime(DateTime.UtcNow);

        var todayScore = await _context.DailyFocusScores
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.UserId == request.UserId && s.Date == targetDate, ct);

        if (todayScore == null)
            return new DailyInsightResponse { HasData = false, Date = targetDate.ToString("yyyy-MM-dd") };

        var thirtyDaysAgo = targetDate.AddDays(-30);
        var baselineScores = await _context.DailyFocusScores
            .IgnoreQueryFilters()
            .Where(s => s.UserId == request.UserId && s.Date >= thirtyDaysAgo && s.Date < targetDate)
            .ToListAsync(ct);

        var avgFocus = baselineScores.Count > 0 ? baselineScores.Average(s => (double)s.FocusScore) : (double?)null;
        var avgProductiveRatio = baselineScores.Count > 0 ? baselineScores.Average(s => s.ProductivityRatio) : (double?)null;
        var avgContextSwitches = baselineScores.Count > 0 ? baselineScores.Average(s => s.ContextSwitchesCount) : (double?)null;

        var focusDiff = avgFocus.HasValue ? todayScore.FocusScore - avgFocus.Value : (double?)null;
        var focusDiffPercent = avgFocus.HasValue && avgFocus.Value > 0
            ? (todayScore.FocusScore - avgFocus.Value) / avgFocus.Value * 100
            : (double?)null;

        var patterns = await _context.UserPatterns
            .IgnoreQueryFilters()
            .Where(p => p.UserId == request.UserId && p.IsActive)
            .OrderByDescending(p => p.Strength)
            .Select(p => new PatternItem { PatternTag = p.PatternTag, Strength = p.Strength, Description = p.Description })
            .ToListAsync(ct);

        var anomalies = await _context.BehavioralAnomalies
            .IgnoreQueryFilters()
            .Where(a => a.UserId == request.UserId && a.DetectedAt == targetDate)
            .Select(a => new AnomalyItem { AnomalyType = a.AnomalyType, Severity = a.Severity })
            .ToListAsync(ct);

        var unreadAlerts = await _context.SmartAlerts
            .IgnoreQueryFilters()
            .Where(a => a.UserId == request.UserId && !a.WasRead)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AlertItem { Id = a.Id, AlertType = a.AlertType, Message = a.Message, Severity = a.Severity, ActionType = a.ActionType })
            .Take(5)
            .ToListAsync(ct);

        var patternTags = patterns.Select(p => p.PatternTag).ToList();
        var suggestion = InsightTextService.GenerateSuggestion(todayScore, avgFocus, focusDiff, patternTags);
        var insightText = InsightTextService.BuildInsightText(todayScore, focusDiff, avgFocus);

        var trend = focusDiff switch { > 5 => "improving", < -5 => "declining", _ => "stable" };

        return new DailyInsightResponse
        {
            HasData = true,
            Date = targetDate.ToString("yyyy-MM-dd"),
            FocusScore = todayScore.FocusScore,
            ProductiveSeconds = todayScore.ProductiveSeconds,
            DistractionSeconds = todayScore.DistractionSeconds,
            NeutralSeconds = todayScore.NeutralSeconds,
            ProductivityRatio = Math.Round(todayScore.ProductivityRatio, 3),
            ContextSwitches = todayScore.ContextSwitchesCount,
            LongestFocusMinutes = todayScore.LongestFocusSeconds / 60,
            Comparison = new DailyInsightComparison
            {
                PersonalAvgFocus = avgFocus.HasValue ? Math.Round(avgFocus.Value, 1) : null,
                FocusDiff = focusDiff.HasValue ? Math.Round(focusDiff.Value, 1) : null,
                FocusDiffPercent = focusDiffPercent.HasValue ? Math.Round(focusDiffPercent.Value, 1) : null,
                Trend = trend,
                BaselineDays = baselineScores.Count,
            },
            Insight = insightText,
            Suggestion = suggestion,
            Patterns = patterns,
            Anomalies = anomalies,
            Alerts = unreadAlerts,
        };
    }
}
