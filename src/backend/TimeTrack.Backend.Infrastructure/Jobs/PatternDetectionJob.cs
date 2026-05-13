using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TimeTrack.Backend.AI.Configuration;
using TimeTrack.Backend.AI.Interfaces;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Jobs;

public sealed class PatternDetectionJob
{
    private readonly TimeTrackDbContext _context;
    private readonly IAIService? _aiService;
    private readonly ILogger<PatternDetectionJob> _logger;
    private readonly PatternDetectionOptions _options;

    public PatternDetectionJob(
        TimeTrackDbContext context,
        IAIService? aiService,
        ILogger<PatternDetectionJob> logger,
        IOptions<PatternDetectionOptions> options)
    {
        _context = context;
        _aiService = aiService;
        _logger = logger;
        _options = options.Value;
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("Starting PatternDetectionJob");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var cutoff = today.AddDays(-_options.StalePatternDays);

        var stalePatterns = await _context.UserPatterns
            .IgnoreQueryFilters()
            .Where(p => p.IsActive && p.DetectedAt < cutoff)
            .ToListAsync();

        foreach (var pattern in stalePatterns)
            pattern.Deactivate();

        if (stalePatterns.Count > 0)
            _logger.LogInformation("Deactivated {Count} stale patterns", stalePatterns.Count);

        var sevenDaysAgo = today.AddDays(-7);
        var usersWithData = await _context.DailyFocusScores
            .IgnoreQueryFilters()
            .Where(d => d.Date >= sevenDaysAgo)
            .GroupBy(d => d.UserId)
            .Where(g => g.Count() >= _options.MinimumDataDays)
            .Select(g => new { UserId = g.Key, OrgId = g.First().OrgId })
            .ToListAsync();

        _logger.LogInformation("Found {Count} users with sufficient data for pattern detection", usersWithData.Count);

        var userIds = usersWithData.Select(u => u.UserId).ToHashSet();
        var allScores = await _context.DailyFocusScores
            .IgnoreQueryFilters()
            .Where(d => userIds.Contains(d.UserId) && d.Date >= sevenDaysAgo)
            .OrderBy(d => d.UserId).ThenBy(d => d.Date)
            .ToListAsync();
        var scoresByUser = allScores.GroupBy(s => s.UserId).ToDictionary(g => g.Key, g => g.ToList());

        var patternsDetected = 0;

        foreach (var user in usersWithData)
        {
            if (!scoresByUser.TryGetValue(user.UserId, out var scores)) continue;
            if (scores.Count < _options.MinimumDataDays) continue;

            var candidates = new[]
            {
                UserPattern.TryDetectMorningProductive(scores, _options.MorningProductiveRatioThreshold),
                UserPattern.TryDetectAfternoonFocusDrop(scores, _options.AfternoonFocusDropMultiplier),
                UserPattern.TryDetectHighContextSwitching(scores, _options.ContextSwitchThreshold),
                UserPattern.TryDetectDeepWorkCapable(scores, _options.DeepWorkSecondsThreshold),
                UserPattern.TryDetectHighDistractionRisk(scores, _options.DistractionRatioThreshold),
            };

            foreach (var candidate in candidates)
            {
                if (candidate == null) continue;
                patternsDetected += await UpsertPatternAsync(
                    user.UserId, user.OrgId, candidate.Tag, today,
                    candidate.Strength, candidate.Evidence);
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("PatternDetectionJob completed. Patterns detected: {Count}", patternsDetected);
    }

    private async Task<int> UpsertPatternAsync(
        Guid userId, Guid orgId, string tag, DateOnly today, double strength, System.Text.Json.JsonElement evidence)
    {
        var existing = await _context.UserPatterns
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.UserId == userId && p.PatternTag == tag && p.DetectedAt == today);

        if (existing != null)
        {
            existing.Reactivate(strength, evidence);
            return 0;
        }

        var pattern = UserPattern.Create(userId, orgId, tag, today, strength, evidence);

        if (_aiService != null)
        {
            try
            {
                var topApps = await _context.ActivitySessions
                    .IgnoreQueryFilters()
                    .Where(a => a.UserId == userId
                        && a.StartedAt >= DateTime.UtcNow.AddDays(-7))
                    .GroupBy(a => a.ProcessName)
                    .OrderByDescending(g => g.Sum(s => s.DurationSeconds))
                    .Select(g => g.Key)
                    .Take(5)
                    .ToListAsync();

                var orgScores = await _context.DailyFocusScores
                    .IgnoreQueryFilters()
                    .Where(d => d.OrgId == orgId && d.Date >= today.AddDays(-7))
                    .GroupBy(d => d.UserId)
                    .ToListAsync();

                double? teamAvgFocus = null;
                if (orgScores.Count > 1)
                {
                    var otherUsersAvg = orgScores
                        .Where(g => g.Key != userId)
                        .SelectMany(g => g)
                        .ToList();
                    if (otherUsersAvg.Count > 0)
                        teamAvgFocus = Math.Round(otherUsersAvg.Average(s => s.FocusScore), 1);
                }

                var context = new UserPatternContext
                {
                    PatternTag = tag,
                    Strength = strength,
                    Evidence = evidence.GetRawText(),
                    UserContext = new UserAiContext
                    {
                        TopApps = topApps,
                        TeamAvgFocusScore = teamAvgFocus,
                        TeamSize = orgScores.Count,
                    },
                };
                var description = await _aiService.DescribePatternAsync(context, orgId, userId);
                pattern.SetDescription(description);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate AI description for pattern {Tag}", tag);
            }
        }

        await _context.UserPatterns.AddAsync(pattern);
        return 1;
    }
}
