using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.AI.Interfaces;
using TimeTrack.Backend.Application.Common.Security;
using TimeTrack.Backend.Application.Insights.DTOs;
using TimeTrack.Backend.Application.Insights.Services;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Api.Insights.Queries;

public sealed record GetReportsInsightsQuery(
    string? StartDate,
    string? EndDate,
    Guid? UserId,
    bool AllTeam,
    Guid CurrentUserId,
    Guid OrgId)
    : IRequest<ReportsInsightsResponse>;

public sealed class GetReportsInsightsQueryHandler
    : IRequestHandler<GetReportsInsightsQuery, ReportsInsightsResponse>
{
    private readonly TimeTrackDbContext _context;
    private readonly IUserAuthorizationService _authorizationService;
    private readonly IUserRepository _userRepository;
    private readonly IAIService _aiService;

    public GetReportsInsightsQueryHandler(
        TimeTrackDbContext context,
        IUserAuthorizationService authorizationService,
        IUserRepository userRepository,
        IAIService aiService)
    {
        _context = context;
        _authorizationService = authorizationService;
        _userRepository = userRepository;
        _aiService = aiService;
    }

    public async Task<ReportsInsightsResponse> Handle(
        GetReportsInsightsQuery request,
        CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var start = !string.IsNullOrEmpty(request.StartDate)
            && DateOnly.TryParse(request.StartDate, CultureInfo.InvariantCulture, out var sd)
            ? sd
            : today.AddDays(-7);

        var end = !string.IsNullOrEmpty(request.EndDate)
            && DateOnly.TryParse(request.EndDate, CultureInfo.InvariantCulture, out var ed)
            ? ed
            : today;

        // Team view
        if (request.AllTeam)
        {
            if (!_authorizationService.IsManagerOrAdmin())
                throw new TimeTrack.Backend.Application.Common.Exceptions.ForbiddenException(
                    "Manager or Admin role required for team view");

            return await BuildTeamInsights(request.OrgId, start, end, ct);
        }

        // Individual view
        var targetUserId = request.UserId ?? request.CurrentUserId;
        if (targetUserId != request.CurrentUserId)
        {
            if (!_authorizationService.CanAccessUserData(targetUserId))
                throw new TimeTrack.Backend.Application.Common.Exceptions.ForbiddenException(
                    "You don't have permission to access this user's data");
        }

        return await BuildIndividualInsights(targetUserId, request.OrgId, start, end, ct);
    }

    private async Task<ReportsInsightsResponse> BuildIndividualInsights(
        Guid targetUserId,
        Guid orgId,
        DateOnly start,
        DateOnly end,
        CancellationToken ct)
    {
        var scores = await _context.DailyFocusScores
            .IgnoreQueryFilters()
            .Where(s => s.UserId == targetUserId && s.Date >= start && s.Date <= end)
            .ToListAsync(ct);

        if (scores.Count == 0)
        {
            return new ReportsInsightsResponse
            {
                HasData = false,
                IsTeamView = false,
                DateRange = new DateRangeInfo
                {
                    StartDate = start.ToString("yyyy-MM-dd"),
                    EndDate = end.ToString("yyyy-MM-dd")
                }
            };
        }

        var avgFocus = scores.Average(s => s.FocusScore);
        var avgContextSwitches = scores.Average(s => s.ContextSwitchesCount);

        // 30-day baseline for trend
        var thirtyDaysAgo = start.AddDays(-30);
        var baselineScores = await _context.DailyFocusScores
            .IgnoreQueryFilters()
            .Where(s => s.UserId == targetUserId && s.Date >= thirtyDaysAgo && s.Date < start)
            .ToListAsync(ct);

        var baselineAvgFocus = baselineScores.Count > 0
            ? baselineScores.Average(s => s.FocusScore)
            : (double?)null;
        var focusDiff = baselineAvgFocus.HasValue
            ? avgFocus - baselineAvgFocus.Value
            : (double?)null;
        var trend = focusDiff switch
        {
            > 5 => "improving",
            < -5 => "declining",
            _ => "stable"
        };

        // Patterns
        var patterns = await _context.UserPatterns
            .IgnoreQueryFilters()
            .Where(p => p.UserId == targetUserId && p.IsActive)
            .OrderByDescending(p => p.Strength)
            .Select(p => new { p.PatternTag, p.Strength, p.Description })
            .Take(5)
            .ToListAsync(ct);

        // Anomalies in range
        var anomalies = await _context.BehavioralAnomalies
            .IgnoreQueryFilters()
            .Where(a => a.UserId == targetUserId && a.DetectedAt >= start && a.DetectedAt <= end)
            .Select(a => new { a.AnomalyType, a.Severity })
            .Take(5)
            .ToListAsync(ct);

        // Unread alerts
        var alerts = await _context.SmartAlerts
            .IgnoreQueryFilters()
            .Where(a => a.UserId == targetUserId && !a.WasRead)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AlertItem
            {
                Id = a.Id,
                AlertType = a.AlertType,
                Message = a.Message,
                Severity = a.Severity,
                ActionType = a.ActionType
            })
            .Take(5)
            .ToListAsync(ct);

        // Benchmark
        var fourWeeksAgo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-28));
        var userWeekly = await _context.FeatureWeeklies
            .IgnoreQueryFilters()
            .Where(w => w.UserId == targetUserId && w.WeekStart >= fourWeeksAgo)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                AvgFocusScore = g.Average(x => x.AvgFocusScore),
                AvgProductiveRatio = g.Average(x => x.AvgProductiveRatio)
            })
            .FirstOrDefaultAsync(ct);

        var orgWeekly = await _context.FeatureWeeklies
            .IgnoreQueryFilters()
            .Where(w => w.OrgId == orgId
                && w.UserId != targetUserId
                && w.WeekStart >= fourWeeksAgo)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                AvgFocusScore = g.Average(x => x.AvgFocusScore),
                AvgProductiveRatio = g.Average(x => x.AvgProductiveRatio),
                UserCount = g.Select(x => x.UserId).Distinct().Count()
            })
            .FirstOrDefaultAsync(ct);

        BenchmarkResponse? benchmark = null;
        if (userWeekly != null)
        {
            var orgFocus = orgWeekly?.AvgFocusScore ?? 0;
            benchmark = new BenchmarkResponse
            {
                HasData = true,
                User = new BenchmarkUser
                {
                    AvgFocusScore = Math.Round(userWeekly.AvgFocusScore, 1),
                    AvgProductiveRatio = Math.Round(userWeekly.AvgProductiveRatio, 3)
                },
                Team = new BenchmarkTeam
                {
                    AvgFocusScore = Math.Round(orgFocus, 1),
                    AvgProductiveRatio = Math.Round(orgWeekly?.AvgProductiveRatio ?? 0, 3),
                    UserCount = orgWeekly?.UserCount ?? 0
                },
                FocusDiff = Math.Round(userWeekly.AvgFocusScore - orgFocus, 1)
            };
        }

        // Top distractions and top apps from ActivitySessions
        var periodStartUtc = DateTime.SpecifyKind(
            start.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var periodEndUtc = DateTime.SpecifyKind(
            end.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

        var topDistractions = await _context.ActivitySessions
            .IgnoreQueryFilters()
            .Where(a => a.UserId == targetUserId
                && a.StartedAt >= periodStartUtc
                && a.StartedAt <= periodEndUtc
                && a.AppCategory == "distraction")
            .GroupBy(a => a.ProcessName)
            .OrderByDescending(g => g.Sum(s => s.DurationSeconds))
            .Select(g => g.Key!)
            .Take(5)
            .ToListAsync(ct);

        var topApps = await _context.ActivitySessions
            .IgnoreQueryFilters()
            .Where(a => a.UserId == targetUserId
                && a.StartedAt >= periodStartUtc
                && a.StartedAt <= periodEndUtc)
            .GroupBy(a => a.ProcessName)
            .OrderByDescending(g => g.Sum(s => s.DurationSeconds))
            .Select(g => g.Key!)
            .Take(5)
            .ToListAsync(ct);

        // Build insight text
        var totalTrackedMinutes = scores.Sum(s =>
            s.ProductiveSeconds + s.DistractionSeconds + s.NeutralSeconds) / 60.0;
        var productiveMinutes = scores.Sum(s => s.ProductiveSeconds) / 60.0;
        var distractionMinutes = scores.Sum(s => s.DistractionSeconds) / 60.0;

        var insightText = $"Periodo de {start:dd/MM} a {end:dd/MM}: "
            + $"{Math.Round(totalTrackedMinutes / 60, 1)}h rastreadas, "
            + $"foco medio {Math.Round(avgFocus)}/100.";
        insightText += $" Produtivas: {Math.Round(productiveMinutes / 60, 1)}h, "
            + $"Distração: {Math.Round(distractionMinutes / 60, 1)}h.";

        if (baselineAvgFocus.HasValue)
        {
            var d = avgFocus - baselineAvgFocus.Value;
            if (d > 5)
                insightText += $" Foco {Math.Abs(d):F0} pontos acima da sua media pessoal.";
            else if (d < -5)
                insightText += $" Foco {Math.Abs(d):F0} pontos abaixo da sua media pessoal.";
        }

        if (topApps.Count > 0)
            insightText += $" Apps mais usados: {string.Join(", ", topApps.Take(3))}.";

        if (topDistractions.Count > 0)
            insightText += $" Principais distrações: {string.Join(", ", topDistractions.Take(3))}.";

        // Try AI suggestion, fallback to deterministic
        var orgMembers = await _userRepository.GetByOrgIdAsync(orgId, ct);

        var suggestionContext = new ReportsSuggestionContext
        {
            FocusScore = avgFocus,
            Trend = trend,
            BaselineFocus = baselineAvgFocus,
            FocusDiff = focusDiff,
            AvgContextSwitches = avgContextSwitches,
            Patterns = patterns.Select(p => new PatternSummary
            {
                Tag = p.PatternTag,
                Strength = p.Strength
            }).ToList(),
            Anomalies = anomalies.Select(a => new AnomalySummary
            {
                Type = a.AnomalyType,
                Severity = a.Severity
            }).ToList(),
            UserContext = new UserAiContext
            {
                TopApps = topApps,
                TeamAvgFocusScore = orgWeekly != null
                    ? Math.Round(orgWeekly.AvgFocusScore, 1)
                    : null,
                TeamSize = orgMembers?.Count() ?? 0,
            },
            TopDistractions = topDistractions,
        };

        string suggestion;
        try
        {
            suggestion = await _aiService.GenerateReportsSuggestionAsync(
                suggestionContext, orgId, targetUserId, ct);
        }
        catch (Exception)
        {
            suggestion = InsightTextService.BuildDynamicSuggestion(
                avgFocus, trend, avgContextSwitches,
                patterns.Select(p => p.PatternTag).ToList(),
                anomalies.Count, topDistractions, topApps);
        }

        return new ReportsInsightsResponse
        {
            HasData = true,
            IsTeamView = false,
            DateRange = new DateRangeInfo
            {
                StartDate = start.ToString("yyyy-MM-dd"),
                EndDate = end.ToString("yyyy-MM-dd")
            },
            FocusScore = Math.Round(avgFocus, 1),
            Insight = insightText,
            Suggestion = suggestion,
            Summary = new ReportsSummary
            {
                TotalTrackedHours = Math.Round(totalTrackedMinutes / 60, 1),
                ProductiveHours = Math.Round(productiveMinutes / 60, 1),
                DistractionHours = Math.Round(distractionMinutes / 60, 1),
                FocusScore = Math.Round(avgFocus, 1),
                ContextSwitchesPerDay = Math.Round(avgContextSwitches, 0),
                Trend = trend
            },
            TopApps = topApps,
            TopDistractions = topDistractions,
            Comparison = new ReportsComparison
            {
                PersonalAvgFocus = baselineAvgFocus.HasValue
                    ? Math.Round(baselineAvgFocus.Value, 1)
                    : null,
                FocusDiff = focusDiff.HasValue
                    ? Math.Round(focusDiff.Value, 1)
                    : null,
                Trend = trend,
                BaselineDays = baselineScores.Count
            },
            Patterns = patterns.Select(p => new PatternItem
            {
                PatternTag = p.PatternTag,
                Strength = p.Strength,
                Description = p.Description
            }).ToList(),
            Anomalies = anomalies.Select(a => new AnomalyItem
            {
                AnomalyType = a.AnomalyType,
                Severity = a.Severity
            }).ToList(),
            Alerts = alerts,
            Benchmark = benchmark
        };
    }

    private async Task<ReportsInsightsResponse> BuildTeamInsights(
        Guid orgId,
        DateOnly start,
        DateOnly end,
        CancellationToken ct)
    {
        var members = await _userRepository.GetByOrgIdAsync(orgId, ct);
        var memberIds = members.Select(m => m.Id).ToList();

        var scores = await _context.DailyFocusScores
            .IgnoreQueryFilters()
            .Where(s => memberIds.Contains(s.UserId) && s.Date >= start && s.Date <= end)
            .GroupBy(s => s.UserId)
            .Select(g => new { UserId = g.Key, AvgFocus = g.Average(s => s.FocusScore) })
            .ToListAsync(ct);

        var topPatterns = await _context.UserPatterns
            .IgnoreQueryFilters()
            .Where(p => memberIds.Contains(p.UserId) && p.IsActive)
            .GroupBy(p => p.PatternTag)
            .OrderByDescending(g => g.Count())
            .Select(g => new
            {
                PatternTag = g.Key,
                Strength = g.Average(p => p.Strength),
                Count = g.Count()
            })
            .Take(3)
            .ToListAsync(ct);

        var totalAnomalies = await _context.BehavioralAnomalies
            .IgnoreQueryFilters()
            .CountAsync(a => memberIds.Contains(a.UserId)
                && a.DetectedAt >= start
                && a.DetectedAt <= end, ct);

        var totalAlerts = await _context.SmartAlerts
            .IgnoreQueryFilters()
            .CountAsync(a => memberIds.Contains(a.UserId) && !a.WasRead, ct);

        var hasData = scores.Count > 0;

        return new ReportsInsightsResponse
        {
            HasData = hasData,
            IsTeamView = true,
            DateRange = new DateRangeInfo
            {
                StartDate = start.ToString("yyyy-MM-dd"),
                EndDate = end.ToString("yyyy-MM-dd")
            },
            TeamSummary = new TeamInsightsSummary
            {
                AvgFocusScore = hasData
                    ? Math.Round(scores.Average(s => s.AvgFocus), 1)
                    : 0,
                MemberCount = scores.Count,
                TopPatterns = topPatterns.Select(p => new TeamPatternItem
                {
                    PatternTag = p.PatternTag,
                    Strength = Math.Round(p.Strength, 2)
                }).ToList(),
                TotalAnomalies = totalAnomalies,
                TotalAlerts = totalAlerts
            }
        };
    }
}
