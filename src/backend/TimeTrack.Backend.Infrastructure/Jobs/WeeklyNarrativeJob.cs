using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TimeTrack.Backend.AI.Configuration;
using TimeTrack.Backend.AI.Interfaces;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Jobs;

public sealed class WeeklyNarrativeJob
{
    private readonly TimeTrackDbContext _context;
    private readonly IAIService _aiService;
    private readonly ILogger<WeeklyNarrativeJob> _logger;
    private readonly NarrativeJobOptions _options;

    public WeeklyNarrativeJob(
        TimeTrackDbContext context,
        IAIService aiService,
        ILogger<WeeklyNarrativeJob> logger,
        IOptions<NarrativeJobOptions> options)
    {
        _context = context;
        _aiService = aiService;
        _logger = logger;
        _options = options.Value;
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("Starting WeeklyNarrativeJob");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var weekStart = GetWeekStart(today);
        var weekEnd = weekStart.AddDays(6);
        var sevenDaysAgo = weekStart;

        var existingCount = await _context.AiDecisionLogs
            .IgnoreQueryFilters()
            .CountAsync(d => d.DecisionType == "weekly_narrative"
                && d.CreatedAt >= weekStart.ToDateTime(TimeOnly.MinValue)
                && d.CreatedAt <= weekEnd.ToDateTime(TimeOnly.MaxValue), default);

        if (existingCount > 0)
        {
            _logger.LogInformation("Weekly narratives already generated for week {WeekStart}", weekStart);
            return;
        }

        var usersWithData = await _context.DailyFocusScores
            .IgnoreQueryFilters()
            .Where(d => d.Date >= sevenDaysAgo && d.Date <= weekEnd)
            .GroupBy(d => d.UserId)
            .Where(g => g.Count() >= _options.MinimumDataDays)
            .Select(g => new { UserId = g.Key, OrgId = g.First().OrgId })
            .ToListAsync();

        _logger.LogInformation("Found {Count} users with sufficient data for weekly narrative", usersWithData.Count);

        var targetUsers = usersWithData.Take(_options.MaxUsersPerRun).ToList();
        var userIds = targetUsers.Select(u => u.UserId).ToHashSet();
        var orgIds = targetUsers.Select(u => u.OrgId).Distinct().ToHashSet();

        // Batch load all data in 5 queries (instead of 6 queries per user)
        var allWeekScores = await _context.DailyFocusScores
            .IgnoreQueryFilters()
            .Where(d => userIds.Contains(d.UserId) && d.Date >= weekStart && d.Date <= weekEnd)
            .ToListAsync();
        var weekScoresByUser = allWeekScores.GroupBy(s => s.UserId).ToDictionary(g => g.Key, g => g.ToList());

        var allPatterns = await _context.UserPatterns
            .IgnoreQueryFilters()
            .Where(p => userIds.Contains(p.UserId) && p.IsActive)
            .OrderByDescending(p => p.Strength)
            .ToListAsync();
        var patternsByUser = allPatterns.GroupBy(p => p.UserId).ToDictionary(g => g.Key, g => g.ToList());

        var allAnomalies = await _context.BehavioralAnomalies
            .IgnoreQueryFilters()
            .Where(a => userIds.Contains(a.UserId) && a.DetectedAt >= weekStart)
            .ToListAsync();
        var anomaliesByUser = allAnomalies.GroupBy(a => a.UserId).ToDictionary(g => g.Key, g => g.ToList());

        var prevWeekStart = weekStart.AddDays(-7);
        var allPrevScores = await _context.DailyFocusScores
            .IgnoreQueryFilters()
            .Where(d => userIds.Contains(d.UserId) && d.Date >= prevWeekStart && d.Date < weekStart)
            .ToListAsync();
        var prevScoresByUser = allPrevScores.GroupBy(s => s.UserId).ToDictionary(g => g.Key, g => g.ToList());

        var weekStartUtc = DateTime.SpecifyKind(weekStart.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var weekEndUtc = DateTime.SpecifyKind(weekEnd.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

        var allSessions = await _context.ActivitySessions
            .IgnoreQueryFilters()
            .Where(a => userIds.Contains(a.UserId) && a.StartedAt >= weekStartUtc && a.StartedAt <= weekEndUtc)
            .GroupBy(a => new { a.UserId, a.ProcessName })
            .Select(g => new { g.Key.UserId, g.Key.ProcessName, TotalDuration = g.Sum(s => s.DurationSeconds) })
            .OrderByDescending(x => x.TotalDuration)
            .ToListAsync();
        var topAppsByUser = allSessions
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.ProcessName).Take(5).ToList());

        var allOrgScores = await _context.DailyFocusScores
            .IgnoreQueryFilters()
            .Where(d => orgIds.Contains(d.OrgId) && d.Date >= weekStart && d.Date <= weekEnd)
            .ToListAsync();
        var orgScoresGrouped = allOrgScores.GroupBy(d => d.OrgId)
            .ToDictionary(g => g.Key, g => g.GroupBy(d => d.UserId).ToList());

        var generated = 0;
        foreach (var user in targetUsers)
        {
            try
            {
                await GenerateNarrativeForUserAsync(
                    user.UserId, user.OrgId, weekStart, weekEnd,
                    weekScoresByUser, patternsByUser, anomaliesByUser,
                    prevScoresByUser, topAppsByUser, orgScoresGrouped);
                generated++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating narrative for user {UserId}", user.UserId);
            }
        }

        _logger.LogInformation("WeeklyNarrativeJob completed. Generated: {Generated}", generated);
    }

    private async Task GenerateNarrativeForUserAsync(
        Guid userId, Guid orgId, DateOnly weekStart, DateOnly weekEnd,
        Dictionary<Guid, List<DailyFocusScore>> weekScoresByUser,
        Dictionary<Guid, List<UserPattern>> patternsByUser,
        Dictionary<Guid, List<BehavioralAnomaly>> anomaliesByUser,
        Dictionary<Guid, List<DailyFocusScore>> prevScoresByUser,
        Dictionary<Guid, List<string>> topAppsByUser,
        Dictionary<Guid, List<IGrouping<Guid, DailyFocusScore>>> orgScoresGrouped)
    {
        if (!weekScoresByUser.TryGetValue(userId, out var scores)) return;
        if (scores.Count < _options.MinimumDataDays) return;

        var patterns = patternsByUser.GetValueOrDefault(userId, [])
            .OrderByDescending(p => p.Strength).Take(3).ToList();

        var anomalies = anomaliesByUser.GetValueOrDefault(userId, []);

        var totalActiveHours = scores.Sum(s => s.ProductiveSeconds + s.DistractionSeconds + s.NeutralSeconds) / 3600.0;
        var avgFocus = scores.Average(s => s.FocusScore);
        var avgProductiveRatio = scores.Count > 0
            ? scores.Average(s =>
            {
                var total = s.ProductiveSeconds + s.DistractionSeconds + s.NeutralSeconds;
                return total > 0 ? (double)s.ProductiveSeconds / total : 0;
            })
            : 0;

        var prevScores = prevScoresByUser.GetValueOrDefault(userId, []);
        double? trendFocus = null;
        double? trendRatio = null;
        double? prevAvgFocus = null;
        if (prevScores.Count >= 2)
        {
            prevAvgFocus = prevScores.Average(s => s.FocusScore);
            trendFocus = Math.Round(avgFocus - prevAvgFocus.Value, 1);

            var prevAvgRatio = prevScores.Average(s =>
            {
                var total = s.ProductiveSeconds + s.DistractionSeconds + s.NeutralSeconds;
                return total > 0 ? (double)s.ProductiveSeconds / total : 0;
            });
            trendRatio = Math.Round(avgProductiveRatio - prevAvgRatio, 3);
        }

        var topApps = topAppsByUser.GetValueOrDefault(userId, []);

        double? teamAvgFocus = null;
        if (orgScoresGrouped.TryGetValue(orgId, out var orgUserGroups))
        {
            var otherUsersAvg = orgUserGroups
                .Where(g => g.Key != userId)
                .SelectMany(g => g)
                .ToList();

            if (otherUsersAvg.Count > 0)
                teamAvgFocus = Math.Round(otherUsersAvg.Average(s => s.FocusScore), 1);
        }

        var context = new WeeklyFeatureContext
        {
            Period = $"{weekStart:dd/MM/yyyy} a {weekEnd:dd/MM/yyyy}",
            Features = new WeeklyFeatures
            {
                TotalActiveHours = Math.Round(totalActiveHours, 1),
                AvgFocusScore = Math.Round(avgFocus, 1),
                AvgProductiveRatio = Math.Round(avgProductiveRatio, 3),
                TrendFocusScore = trendFocus,
                TrendProductiveRatio = trendRatio,
            },
            Patterns = patterns.Select(p => new PatternSummary
            {
                Tag = p.PatternTag,
                Strength = p.Strength,
            }).ToList(),
            Anomalies = anomalies.Select(a => new AnomalySummary
            {
                Type = a.AnomalyType,
                Severity = a.Severity,
            }).ToList(),
            UserContext = new UserAiContext
            {
                TopApps = topApps,
                TeamAvgFocusScore = teamAvgFocus,
                PreviousPeriodFocus = prevAvgFocus.HasValue ? Math.Round(prevAvgFocus.Value, 1) : null,
                TeamSize = orgScoresGrouped.GetValueOrDefault(orgId, []).Count,
            },
        };

        var narrative = await _aiService.GenerateWeeklyNarrativeAsync(context, orgId, userId);
        _logger.LogInformation("Generated narrative for user {UserId}: {Narrative}", userId, narrative[..Math.Min(80, narrative.Length)]);
    }

    private static DateOnly GetWeekStart(DateOnly date)
    {
        var dayOfWeek = (int)date.DayOfWeek;
        var monday = dayOfWeek == 0 ? -6 : 1 - dayOfWeek;
        return date.AddDays(monday);
    }
}
