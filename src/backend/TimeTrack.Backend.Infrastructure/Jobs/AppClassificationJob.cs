using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TimeTrack.Backend.AI.Configuration;
using TimeTrack.Backend.AI.Interfaces;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;
using TimeTrack.Backend.Infrastructure.Jobs.Interfaces;
using TimeTrack.Backend.Infrastructure.Persistence;
using FeedbackExample = TimeTrack.Backend.AI.Interfaces.FeedbackExample;

namespace TimeTrack.Backend.Infrastructure.Jobs;

public sealed class AppClassificationJob : IAppClassificationJob
{
    private readonly TimeTrackDbContext _context;
    private readonly IAIService _aiService;
    private readonly IAiDecisionLogRepository _decisionLogRepo;
    private readonly IAppCategoryOverrideRepository _overrideRepo;
    private readonly ILogger<AppClassificationJob> _logger;
    private readonly ClassificationJobOptions _options;

    private static readonly HashSet<string> BrowserNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "firefox", "brave", "opera", "vivaldi", "safari", "arc"
    };

    public AppClassificationJob(
        TimeTrackDbContext context,
        IAIService aiService,
        IAiDecisionLogRepository decisionLogRepo,
        IAppCategoryOverrideRepository overrideRepo,
        ILogger<AppClassificationJob> logger,
        IOptions<ClassificationJobOptions> options)
    {
        _context = context;
        _aiService = aiService;
        _decisionLogRepo = decisionLogRepo;
        _overrideRepo = overrideRepo;
        _logger = logger;
        _options = options.Value;
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("Starting AppClassificationJob");

        var cutoff = DateTime.UtcNow.AddDays(-_options.RecentRejectionDays);

        var neutralApps = await _context.AppCategoryGlobals
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(a => a.Productivity == AppProductivityCategory.Neutral)
            .Select(a => a.Identifier)
            .ToListAsync();

        var recentRejections = new HashSet<string>(await _context.AiDecisionLogs
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(d => d.DecisionType == "app_classification"
                       && d.ReviewOutcome == "rejected"
                       && d.CreatedAt >= cutoff)
            .Select(d => d.InputData.GetProperty("exe_name").GetString() ?? "")
            .Where(s => !string.IsNullOrEmpty(s))
            .ToListAsync());

        var pendingApps = new HashSet<string>(await _context.AiDecisionLogs
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(d => d.DecisionType == "app_classification" && !d.WasReviewed)
            .Select(d => d.InputData.GetProperty("exe_name").GetString() ?? "")
            .Where(s => !string.IsNullOrEmpty(s))
            .ToListAsync());

        var candidates = neutralApps
            .Where(a => !recentRejections.Contains(a) && !pendingApps.Contains(a))
            .ToList();

        _logger.LogInformation("Found {Total} neutral apps, {Candidates} candidates for classification",
            neutralApps.Count, candidates.Count);

        var targetCandidates = candidates.Take(_options.MaxAppsPerRun).ToList();

        if (targetCandidates.Count == 0)
        {
            _logger.LogInformation("No candidates to classify");
            return;
        }

        // Batch load data for all candidates in 3 queries (instead of 3 per app)
        var candidateSet = new HashSet<string>(targetCandidates, StringComparer.OrdinalIgnoreCase);
        var last30Days = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var cutoffDate = DateTime.UtcNow.AddDays(-30);

        var allAppScores = await _context.DailyFocusScores
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(d => d.Date >= last30Days && candidateSet.Contains(d.TopAppExe))
            .ToListAsync();
        var scoresByApp = allAppScores
            .GroupBy(s => s.TopAppExe, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList());

        var allTitles = await _context.ActivitySessions
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(a => candidateSet.Contains(a.ProcessName) && a.StartedAt >= cutoffDate)
            .GroupBy(a => new { a.ProcessName, a.WindowTitle })
            .Select(g => new { g.Key.ProcessName, g.Key.WindowTitle, Count = g.Count() })
            .ToListAsync();
        var titlesByApp = allTitles
            .GroupBy(x => x.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.Count).Select(x => x.WindowTitle).Where(t => !string.IsNullOrEmpty(t)).Take(5).ToList()!,
                StringComparer.OrdinalIgnoreCase);

        var coOccurring = await _context.ActivitySessions
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(a => a.StartedAt >= cutoffDate && !candidateSet.Contains(a.ProcessName))
            .GroupBy(a => a.ProcessName)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .Take(5)
            .ToListAsync();

        // Corrections are the same for all apps - load once
        var corrections = await GetRecentCorrectionsAsync(5);

        var processed = 0;
        foreach (var identifier in targetCandidates)
        {
            try
            {
                await ClassifyAppAsync(identifier, scoresByApp, titlesByApp, coOccurring, corrections);
                processed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error classifying app {Identifier}", identifier);
            }
        }

        _logger.LogInformation("AppClassificationJob completed. Processed: {Processed}", processed);
    }

    private async Task ClassifyAppAsync(
        string identifier,
        Dictionary<string, List<DailyFocusScore>> scoresByApp,
        Dictionary<string, List<string>> titlesByApp,
        List<string> coOccurring,
        List<FeedbackExample> corrections)
    {
        var dailyScores = scoresByApp.GetValueOrDefault(identifier, []);
        var sampleTitles = titlesByApp.GetValueOrDefault(identifier, []);

        var avgDailySeconds = dailyScores.Count > 0
            ? (int)dailyScores.Average(d => d.TopAppSeconds)
            : 0;

        var request = new AppClassificationRequest
        {
            ExeName = identifier,
            SampleWindowTitles = sampleTitles,
            AvgDailySeconds = avgDailySeconds,
            UsageDaysLast30 = dailyScores.Count,
            CorrelationWithFocusScore = dailyScores.Count >= 5
                ? CalculateCorrelation(dailyScores)
                : null,
            CoOccurringApps = coOccurring,
            FewShotExamples = corrections
        };

        var result = await _aiService.ClassifyAppAsync(request, Guid.Empty);

        _logger.LogInformation("Classified {Identifier}: {Category}/{Subcategory} ({Confidence:P0})",
            identifier, result.Category, result.Subcategory, result.Confidence);
    }

    private async Task<List<FeedbackExample>> GetRecentCorrectionsAsync(int limit)
    {
        var cutoff = DateTime.UtcNow.AddDays(-30);

        var corrected = await _context.AiDecisionLogs
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(d => d.DecisionType == "app_classification"
                       && d.WasReviewed
                       && d.ReviewOutcome == "corrected"
                       && d.CreatedAt >= cutoff)
            .OrderByDescending(d => d.CreatedAt)
            .Take(limit)
            .ToListAsync();

        var examples = new List<FeedbackExample>();
        foreach (var log in corrected)
        {
            var exeName = log.InputData.ValueKind == JsonValueKind.Object
                && log.InputData.TryGetProperty("exe_name", out var exeEl)
                ? exeEl.GetString()
                : null;

            var correctCategory = log.CorrectValue.HasValue
                && log.CorrectValue.Value.ValueKind == JsonValueKind.Object
                && log.CorrectValue.Value.TryGetProperty("category", out var catEl)
                ? catEl.GetString()
                : null;

            if (!string.IsNullOrEmpty(exeName) && !string.IsNullOrEmpty(correctCategory))
            {
                examples.Add(new FeedbackExample
                {
                    ExeName = exeName,
                    CorrectCategory = correctCategory,
                    Reason = "Admin corrected this classification"
                });
            }
        }

        return examples;
    }

    private static double? CalculateCorrelation(List<DailyFocusScore> scores)
    {
        if (scores.Count < 5) return null;

        var focusScores = scores.Select(s => (double)s.FocusScore).ToList();
        var appTimes = scores.Select(s => (double)s.TopAppSeconds).ToList();

        var n = focusScores.Count;
        var sumX = focusScores.Sum();
        var sumY = appTimes.Sum();
        var sumXY = focusScores.Zip(appTimes, (x, y) => x * y).Sum();
        var sumX2 = focusScores.Sum(x => x * x);
        var sumY2 = appTimes.Sum(y => y * y);

        var denominator = Math.Sqrt((n * sumX2 - sumX * sumX) * (n * sumY2 - sumY * sumY));
        if (denominator == 0) return null;

        return (n * sumXY - sumX * sumY) / denominator;
    }
}
