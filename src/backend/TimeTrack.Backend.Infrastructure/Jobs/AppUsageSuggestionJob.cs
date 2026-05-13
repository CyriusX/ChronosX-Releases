using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.AI.Interfaces;
using TimeTrack.Backend.Domain.Constants;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.ValueObjects;
using TimeTrack.Backend.Infrastructure.Jobs.Interfaces;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Jobs;

public sealed class AppUsageSuggestionJob : IAppUsageSuggestionJob
{
    private readonly TimeTrackDbContext _context;
    private readonly IAIService? _aiService;
    private readonly ILogger<AppUsageSuggestionJob> _logger;

    private const int AnalysisWindowDays = 7;
    private const int MaxSuggestionsPerOrg = 20;
    private const double MinHoursPerUserPerWeek = 0.5;
    private const double MinOrgHoursPerWeek = 1.0;

    private static readonly HashSet<string> DistractionTitleKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "youtube", "reddit", "tiktok", "instagram", "facebook", "twitter", "x.com",
        "netflix", "twitch", "prime video", "disney+", "hulu", "spotify - web",
        "9gag", "pin terest", "pinterest", "tumblr",
    };

    private static readonly HashSet<string> ProductiveTitleKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "visual studio", "vscode", "intellij", "webstorm", "rider", "xcode",
        "figma", "sketch", "adobe", "photoshop", "illustrator", "canva",
        "jira", "linear", "notion", "confluence", "trello", "asana",
        "github", "gitlab", "bitbucket", "docker", "kubernetes",
        "stack overflow", "stackoverflow", "docs.microsoft",
    };

    private static readonly HashSet<string> DevToolProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "code", "devenv", "idea64", "webstorm64", "rider64", "xcode",
        "cursor", "windsurf", "zed", "neovim", "vim",
        "visual studio code", "vscode", "visual studio",
    };

    private static readonly HashSet<string> DesignToolProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "figma", "sketch", "photoshop", "illustrator", "canva", "inkscape",
    };

    private static readonly HashSet<string> GameProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "cs2", "valorant", "valorant-win64-shipping", "league of legends",
        "fortnite", "minecraft", "steam", "epic games", "battlefield",
        "overwatch", "apex legends", "dota2", "csgo",
    };

    private static readonly HashSet<string> DistractionProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "youtube", "netflix", "twitch", "tiktok", "instagram",
        "facebook", "twitter", "reddit", "9gag",
    };

    public AppUsageSuggestionJob(
        TimeTrackDbContext context,
        IAIService? aiService,
        ILogger<AppUsageSuggestionJob> logger)
    {
        _context = context;
        _aiService = aiService;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("Starting AppUsageSuggestionJob");

        var cutoff = DateTime.UtcNow.AddDays(-AnalysisWindowDays);

        var orgs = await _context.ActivitySessions
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(a => a.StartedAt >= cutoff)
            .Select(a => a.OrgId)
            .Distinct()
            .ToListAsync();

        _logger.LogInformation("Found {Count} orgs with activity in the last {Days} days", orgs.Count, AnalysisWindowDays);

        var totalSuggestions = 0;
        foreach (var orgId in orgs)
        {
            try
            {
                var count = await AnalyzeOrgAsync(orgId, cutoff);
                totalSuggestions += count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing org {OrgId} for usage suggestions", orgId);
            }
        }

        _logger.LogInformation("AppUsageSuggestionJob completed. Created {Count} suggestions across {Orgs} orgs",
            totalSuggestions, orgs.Count);
    }

    public async Task ExecuteForOrgAsync(Guid orgId)
    {
        var cutoff = DateTime.UtcNow.AddDays(-AnalysisWindowDays);
        var count = await AnalyzeOrgAsync(orgId, cutoff);
        _logger.LogInformation("Created {Count} usage suggestions for org {OrgId}", count, orgId);
    }

    private async Task<int> AnalyzeOrgAsync(Guid orgId, DateTime cutoff)
    {
        // Load users for this org (for names)
        var users = await _context.Users
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(u => u.OrgId == orgId && u.Status == Domain.ValueObjects.UserStatus.Active)
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName);

        // Aggregate sessions by process name
        var sessions = await _context.ActivitySessions
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(a => a.OrgId == orgId && a.StartedAt >= cutoff)
            .ToListAsync();

        // Filter internal apps in memory (EF Core can't translate InternalApps.IsInternal)
        sessions = sessions.Where(a => !InternalApps.IsInternal(a.ProcessName)).ToList();

        if (sessions.Count == 0) return 0;

        var appGroups = sessions
            .GroupBy(s => s.ProcessName)
            .Select(g =>
            {
                var totalSeconds = g.Sum(s => s.DurationSeconds);
                var distinctUsers = g.Select(s => s.UserId).Distinct().ToList();
                var windowTitles = g
                    .Where(s => !string.IsNullOrEmpty(s.WindowTitle))
                    .Select(s => s.WindowTitle!)
                    .Distinct()
                    .Take(5)
                    .ToList();
                var currentCategory = g.FirstOrDefault()?.AppCategory ?? "neutral";
                var perUser = g
                    .GroupBy(s => s.UserId)
                    .Select(ug => new UserUsage(
                        ug.Key,
                        ug.Sum(s => s.DurationSeconds) / 3600.0))
                    .OrderByDescending(u => u.Hours)
                    .ToList();

                return new AppUsageAggregate(
                    ProcessName: g.Key,
                    TotalHours: totalSeconds / 3600.0,
                    DistinctUsers: distinctUsers.Count,
                    WindowTitles: windowTitles,
                    CurrentCategory: currentCategory,
                    PerUserBreakdown: perUser);
            })
            .Where(a => a.TotalHours >= MinOrgHoursPerWeek || a.PerUserBreakdown.Any(u => u.Hours >= MinHoursPerUserPerWeek))
            .OrderByDescending(a => a.TotalHours)
            .ToList();

        // Filter: only apps that qualify for suggestion
        var candidates = new List<(AppUsageAggregate App, string SuggestedCategory, string SuggestedSubcategory, double Confidence, string Reasoning)>();

        _logger.LogInformation("AppUsageSuggestion: Org {OrgId} has {Count} app groups after filtering", orgId, appGroups.Count);

        foreach (var app in appGroups)
        {
            var suggestion = EvaluateCandidate(app);
            _logger.LogDebug("AppUsageSuggestion: {Process} ({Category}, {Hours:F1}h) → suggestion={HasSuggestion}",
                app.ProcessName, app.CurrentCategory, app.TotalHours, suggestion != null);
            if (suggestion != null)
                candidates.Add((app, suggestion.Value.Category, suggestion.Value.Subcategory, suggestion.Value.Confidence, suggestion.Value.Reasoning));
        }

        // Dedup: skip apps with pending suggestions for this org
        var pendingExeNames = await _context.AiDecisionLogs
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(d => d.OrgId == orgId
                && d.DecisionType == "app_usage_suggestion"
                && !d.WasReviewed)
            .Select(d => d.InputData.GetProperty("exe_name").GetString() ?? "")
            .Where(s => !string.IsNullOrEmpty(s))
            .ToListAsync();

        var pendingSet = new HashSet<string>(pendingExeNames, StringComparer.OrdinalIgnoreCase);

        // Also skip apps that already have an override for this org
        var overrideIdentifiers = await _context.AppCategoryOverrides
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(o => o.OrgId == orgId)
            .Select(o => o.Identifier)
            .ToListAsync();

        var overrideSet = new HashSet<string>(overrideIdentifiers, StringComparer.OrdinalIgnoreCase);

        var filtered = candidates
            .Where(c => !pendingSet.Contains(c.App.ProcessName) && !overrideSet.Contains(c.App.ProcessName))
            .Take(MaxSuggestionsPerOrg)
            .ToList();

        _logger.LogInformation("AppUsageSuggestion: Org {OrgId} — candidates={Candidates}, filtered={Filtered}, pending={Pending}, overrides={Overrides}",
            orgId, candidates.Count, filtered.Count, pendingSet.Count, overrideSet.Count);

        var created = 0;
        foreach (var (app, category, subcategory, confidence, reasoning) in filtered)
        {
            try
            {
                await CreateSuggestionAsync(orgId, app, category, subcategory, confidence, reasoning, users);
                created++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating usage suggestion for {Process} in org {OrgId}", app.ProcessName, orgId);
            }
        }

        return created;
    }

    private (string Category, string Subcategory, double Confidence, string Reasoning)? EvaluateCandidate(AppUsageAggregate app)
    {
        var exeLower = app.ProcessName.ToLowerInvariant();
        var exeNoExt = exeLower.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);

        var topUsers = string.Join(", ", app.PerUserBreakdown.Take(3).Select(u => $"{u.Hours:F1}h"));

        // Heuristic: dev tool → productive
        if (DevToolProcesses.Any(d => exeNoExt.Contains(d, StringComparison.OrdinalIgnoreCase)) && app.CurrentCategory is "neutral" or null)
        {
            return ("productive", "development", 0.9,
                $"Ferramenta de desenvolvimento usada ativamente ({app.TotalHours:F1}h/semana). Top uso: {topUsers}.");
        }

        // Heuristic: design tool → productive
        if (DesignToolProcesses.Any(d => exeNoExt.Contains(d, StringComparison.OrdinalIgnoreCase)) && app.CurrentCategory is "neutral" or null)
        {
            return ("productive", "design", 0.9,
                $"Ferramenta de design usada ativamente ({app.TotalHours:F1}h/semana). Top uso: {topUsers}.");
        }

        // Heuristic: game → distraction
        if (GameProcesses.Any(g => exeNoExt.Contains(g, StringComparison.OrdinalIgnoreCase)) && app.CurrentCategory is not "distraction")
        {
            return ("distraction", "gaming", 0.95,
                $"Jogo detectado ({app.TotalHours:F1}h/semana). Recomendado classificar como distracao. Top uso: {topUsers}.");
        }

        // Heuristic: distraction app in process name
        if (DistractionProcesses.Any(d => exeNoExt.Contains(d, StringComparison.OrdinalIgnoreCase)) && app.CurrentCategory is not "distraction")
        {
            return ("distraction", "entertainment", 0.85,
                $"App de entretenimento/distracao ({app.TotalHours:F1}h/semana). Top uso: {topUsers}.");
        }

        // Heuristic: window titles with distraction keywords
        var titleText = string.Join(" ", app.WindowTitles).ToLowerInvariant();
        var hasDistractionTitles = DistractionTitleKeywords.Any(k => titleText.Contains(k));

        if (hasDistractionTitles && app.CurrentCategory is "neutral" or "productive")
        {
            var matchedTitles = app.WindowTitles
                .Where(t => DistractionTitleKeywords.Any(k => t.Contains(k, StringComparison.OrdinalIgnoreCase)))
                .Take(3);
            return ("distraction", "entertainment", 0.8,
                $"Titulos de janela sugerem conteudo de entretenimento: {string.Join(", ", matchedTitles)}. Uso: {app.TotalHours:F1}h/semana. Top: {topUsers}.");
        }

        // Heuristic: window titles with productive keywords
        var hasProductiveTitles = ProductiveTitleKeywords.Any(k => titleText.Contains(k));

        if (hasProductiveTitles && app.CurrentCategory is "neutral" or null)
        {
            var matchedTitles = app.WindowTitles
                .Where(t => ProductiveTitleKeywords.Any(k => t.Contains(k, StringComparison.OrdinalIgnoreCase)))
                .Take(3);
            return ("productive", "productivity_tools", 0.75,
                $"Titulos de janela sugerem uso produtivo: {string.Join(", ", matchedTitles)}. Uso: {app.TotalHours:F1}h/semana. Top: {topUsers}.");
        }

        // Catch-all: any neutral app with meaningful usage → send to AI for classification
        if (app.CurrentCategory is "neutral" or null
            && (app.TotalHours >= MinOrgHoursPerWeek || app.PerUserBreakdown.Any(u => u.Hours >= MinHoursPerUserPerWeek)))
        {
            var titlesPreview = app.WindowTitles.Count > 0
                ? string.Join(", ", app.WindowTitles.Take(3))
                : "sem titulos";
            return ("neutral", "unknown", 0.3,
                $"App neutro com uso relevante ({app.TotalHours:F1}h/semana). Titulos: {titlesPreview}. Top: {topUsers}. Enviado para classificacao via AI.");
        }

        return null;
    }

    private async Task CreateSuggestionAsync(
        Guid orgId,
        AppUsageAggregate app,
        string suggestedCategory,
        string suggestedSubcategory,
        double confidence,
        string fallbackReasoning,
        Dictionary<Guid, string> users)
    {
        var perUserEntries = app.PerUserBreakdown
            .Take(5)
            .Select(u => new
            {
                user_id = u.UserId.ToString(),
                user_name = users.TryGetValue(u.UserId, out var name) ? name : "Desconhecido",
                hours = Math.Round(u.Hours, 1)
            })
            .ToList();

        var inputData = JsonSerializer.SerializeToElement(new
        {
            exe_name = app.ProcessName,
            current_category = app.CurrentCategory ?? "neutral",
            total_org_hours = Math.Round(app.TotalHours, 1),
            sample_window_titles = app.WindowTitles,
            per_user_breakdown = perUserEntries
        });

        // Try AI for higher confidence suggestions
        string reasoning = fallbackReasoning;
        string finalCategory = suggestedCategory;
        string finalSubcategory = suggestedSubcategory;
        double finalConfidence = confidence;

        if (confidence < 0.9 && _aiService != null)
        {
            try
            {
                var aiRequest = new AppClassificationRequest
                {
                    ExeName = app.ProcessName,
                    SampleWindowTitles = app.WindowTitles,
                    AvgDailySeconds = (int)(app.TotalHours * 3600 / AnalysisWindowDays),
                    UsageDaysLast30 = AnalysisWindowDays,
                    PerUserBreakdown = perUserEntries.Select(u => new UserUsageEntry
                    {
                        UserName = u.user_name,
                        Hours = u.hours
                    }).ToList()
                };

                var result = await _aiService.ClassifyAppAsync(aiRequest, orgId);
                finalCategory = result.Category;
                finalSubcategory = result.Subcategory;
                finalConfidence = result.Confidence;
                reasoning = result.Reasoning;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI classification failed for {Process}, using heuristic fallback", app.ProcessName);
            }
        }

        var output = JsonSerializer.SerializeToElement(new
        {
            category = finalCategory,
            subcategory = finalSubcategory,
            confidence = finalConfidence,
            reasoning
        });

        var log = AiDecisionLog.Create(
            orgId: orgId,
            decisionType: "app_usage_suggestion",
            inputData: inputData,
            output: output,
            modelVersion: "usage-heuristic-v1",
            confidence: finalConfidence);

        await _context.AiDecisionLogs.AddAsync(log);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Created usage suggestion for {Process} in org {OrgId}: {Category}/{Subcategory} ({Confidence:P0})",
            app.ProcessName, orgId, finalCategory, finalSubcategory, finalConfidence);
    }

    private sealed record UserUsage(
        Guid UserId,
        double Hours);

    private sealed record AppUsageAggregate(
        string ProcessName,
        double TotalHours,
        int DistinctUsers,
        List<string> WindowTitles,
        string? CurrentCategory,
        List<UserUsage> PerUserBreakdown);
}
