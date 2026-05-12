using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimeTrack.Api.Security;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Infrastructure.Jobs;
using TimeTrack.Backend.Infrastructure.Jobs.Interfaces;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Api.Controllers;

[ApiController]
[Route("api/v1/internal")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class AiTestDataController : ControllerBase
{
    private readonly TimeTrackDbContext _context;
    private readonly ICurrentUserContext _currentUser;

    public AiTestDataController(TimeTrackDbContext context, ICurrentUserContext currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Generates 30 days of synthetic DailyFocusScore data for the current user.
    /// </summary>
    [HttpPost("seed-ai-data")]
    public async Task<IActionResult> SeedAiData(CancellationToken ct)
    {
        if (_currentUser.UserId == null || _currentUser.OrgId == null)
            return Forbid();

        var userId = _currentUser.UserId.Value;
        var orgId = _currentUser.OrgId.Value;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Check if data already exists
        var existing = await _context.DailyFocusScores
            .IgnoreQueryFilters()
            .CountAsync(s => s.UserId == userId && s.Date >= today.AddDays(-30), ct);

        if (existing >= 25)
            return BadRequest(new { message = "User already has sufficient data. Clear existing data first." });

        var rng = new Random(userId.GetHashCode());
        var scores = new List<DailyFocusScore>();

        for (var i = 29; i >= 0; i--)
        {
            var date = today.AddDays(-i);
            var isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

            // Realistic patterns: higher productivity on weekdays, lower on weekends
            var baseProductive = isWeekend ? rng.Next(3600, 14400) : rng.Next(18000, 28800);
            var baseDistraction = isWeekend ? rng.Next(1800, 7200) : rng.Next(600, 3600);
            var baseNeutral = rng.Next(600, 3600);
            var totalActive = baseProductive + baseDistraction + baseNeutral;

            // Focus score correlates with productivity ratio
            var productiveRatio = totalActive > 0 ? (double)baseProductive / totalActive : 0;
            var focusScore = (short)Math.Clamp(
                Math.Round(productiveRatio * 100 + rng.Next(-10, 10)), 0, 100);

            // Context switches: higher on chaotic days
            var contextSwitches = isWeekend
                ? rng.Next(5, 20)
                : rng.Next(10, rng.Next(15, 60));

            // Simulate some days with specific patterns
            // Day -3: overwork (lots of productive time)
            if (i == 3) { baseProductive = 14400; totalActive = baseProductive + baseDistraction + baseNeutral; }

            // Day -1: slight productivity drop
            if (i == 1) { focusScore = (short)Math.Max(20, focusScore - 25); baseDistraction += 3000; totalActive = baseProductive + baseDistraction + baseNeutral; }

            var totalMs = totalActive * 1000L;
            var score = DailyFocusScore.Create(
                Guid.NewGuid(), orgId, userId, date,
                totalMs, baseProductive * 1000L, baseDistraction * 1000L,
                0, 0, 0, 0, focusScore);

            score.UpdateFeatures(
                baseProductive, baseDistraction, baseNeutral,
                contextSwitches,
                rng.Next(0, 5),
                "code.exe",
                baseProductive / 2,
                rng.Next(3, 12),
                rng.Next(0, 3600),
                rng.Next(1, 4),
                rng.Next(0, 3),
                rng.Next(1800, 7200),
                rng.Next(900, 3600));

            scores.Add(score);
        }

        _context.DailyFocusScores.AddRange(scores);
        await _context.SaveChangesAsync(ct);

        return Ok(new
        {
            message = "30 days of synthetic data generated",
            userId,
            daysGenerated = scores.Count,
            dateRange = $"{scores.First().Date:yyyy-MM-dd} to {scores.Last().Date:yyyy-MM-dd}"
        });
    }

    /// <summary>
    /// Triggers AI jobs sequentially (Pattern → Anomaly → Threshold → Alert).
    /// </summary>
    [HttpPost("trigger-ai-jobs")]
    [AllowAnonymous]
    public async Task<IActionResult> TriggerAiJobs(
        [FromServices] PatternDetectionJob patternJob,
        [FromServices] AnomalyDetectionJob anomalyJob,
        [FromServices] ThresholdUpdateJob thresholdJob,
        [FromServices] AlertGenerationJob alertJob,
        [FromServices] LiveInsightJob liveInsightJob,
        [FromServices] IAppUsageSuggestionJob appUsageSuggestionJob,
        CancellationToken ct)
    {
        var results = new List<string>();

        try
        {
            await patternJob.ExecuteAsync();
            results.Add("PatternDetection: OK");
        }
        catch (Exception ex)
        {
            results.Add($"PatternDetection: FAILED - {ex.Message}");
        }

        try
        {
            await anomalyJob.ExecuteAsync();
            results.Add("AnomalyDetection: OK");
        }
        catch (Exception ex)
        {
            results.Add($"AnomalyDetection: FAILED - {ex.Message}");
        }

        try
        {
            await thresholdJob.ExecuteAsync();
            results.Add("ThresholdUpdate: OK");
        }
        catch (Exception ex)
        {
            results.Add($"ThresholdUpdate: FAILED - {ex.Message}");
        }

        try
        {
            await alertJob.ExecuteAsync();
            results.Add("AlertGeneration: OK");
        }
        catch (Exception ex)
        {
            results.Add($"AlertGeneration: FAILED - {ex.Message}");
        }

        try
        {
            await liveInsightJob.ExecuteAsync();
            results.Add("LiveInsight: OK");
        }
        catch (Exception ex)
        {
            results.Add($"LiveInsight: FAILED - {ex.Message}");
        }

        try
        {
            await appUsageSuggestionJob.ExecuteAsync();
            results.Add("AppUsageSuggestion: OK");
        }
        catch (Exception ex)
        {
            results.Add($"AppUsageSuggestion: FAILED - {ex.Message}");
        }

        return Ok(new { message = "AI jobs executed", results });
    }

    [HttpPost("trigger-usage-suggestions")]
    [AllowAnonymous]
    public async Task<IActionResult> TriggerUsageSuggestions(
        [FromServices] IAppUsageSuggestionJob job,
        CancellationToken ct)
    {
        try
        {
            await job.ExecuteAsync();

            // Check what was created
            var newSuggestionsRaw = await _context.AiDecisionLogs
                .IgnoreQueryFilters()
                .Where(d => d.DecisionType == "app_usage_suggestion")
                .OrderByDescending(d => d.CreatedAt)
                .Take(10)
                .ToListAsync(ct);

            var newSuggestions = newSuggestionsRaw.Select(d => new
            {
                d.Id,
                ExeName = d.InputData.TryGetProperty("exe_name", out var exeEl) ? exeEl.GetString() : null,
                Category = d.Output.TryGetProperty("category", out var catEl) ? catEl.GetString() : null,
                d.Confidence,
                d.CreatedAt
            }).ToList();

            return Ok(new { message = "AppUsageSuggestionJob executed", createdRecent = newSuggestions.Count, suggestions = newSuggestions });
        }
        catch (Exception ex)
        {
            return Ok(new { message = $"FAILED: {ex.Message}", stack = ex.StackTrace?.Split('\n').Take(5) });
        }
    }

    [HttpGet("usage-suggestions")]
    [AllowAnonymous]
    public async Task<IActionResult> GetUsageSuggestions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var validTypes = new HashSet<string> { "app_usage_suggestion" };

        var query = _context.AiDecisionLogs
            .IgnoreQueryFilters()
            .Where(d => !d.WasReviewed && d.DecisionType == "app_usage_suggestion")
            .OrderByDescending(d => d.CreatedAt);

        var total = await query.CountAsync(ct);

        var rawItems = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // Debug: check if ActivitySessions exist in last 7 days
        var sessionCount = await _context.ActivitySessions
            .IgnoreQueryFilters()
            .Where(a => a.StartedAt >= DateTime.UtcNow.AddDays(-7))
            .CountAsync(ct);

        var topAppsRaw = await _context.ActivitySessions
            .IgnoreQueryFilters()
            .Where(a => a.StartedAt >= DateTime.UtcNow.AddDays(-7))
            .GroupBy(a => a.ProcessName)
            .Select(g => new { Process = g.Key, TotalSeconds = g.Sum(s => s.DurationSeconds), Count = g.Count() })
            .OrderByDescending(x => x.TotalSeconds)
            .Take(10)
            .ToListAsync(ct);

        var distinctApps = topAppsRaw.Select(x => new { Process = x.Process, Hours = Math.Round(x.TotalSeconds / 3600.0, 1), Sessions = x.Count }).ToList();

        // Check overrides
        var overrideCount = await _context.AppCategoryOverrides
            .IgnoreQueryFilters()
            .CountAsync(ct);

        var overrideSamples = await _context.AppCategoryOverrides
            .IgnoreQueryFilters()
            .Select(o => new { o.Identifier, o.Productivity })
            .Take(20)
            .ToListAsync(ct);

        // Check app categories for top apps
        var recentSessions = await _context.ActivitySessions
            .IgnoreQueryFilters()
            .Where(a => a.StartedAt >= DateTime.UtcNow.AddDays(-7))
            .Select(a => new { a.ProcessName, a.AppCategory, a.DurationSeconds })
            .ToListAsync(ct);

        var topAppCategories = recentSessions
            .GroupBy(a => new { a.ProcessName, a.AppCategory })
            .Select(g => new { Process = g.Key.ProcessName, Category = g.Key.AppCategory, Hours = Math.Round(g.Sum(s => s.DurationSeconds) / 3600.0, 1) })
            .OrderByDescending(x => x.Hours)
            .Take(15)
            .ToList();

        var items = rawItems.Select(d => new
        {
            d.Id,
            ExeName = d.InputData.TryGetProperty("exe_name", out var exeEl) ? exeEl.GetString() : null,
            CurrentCategory = d.InputData.TryGetProperty("current_category", out var cc) ? cc.GetString() : null,
            SuggestedCategory = d.Output.TryGetProperty("category", out var catEl) ? catEl.GetString() : null,
            TotalOrgHours = d.InputData.TryGetProperty("total_org_hours", out var toh) ? toh.GetDouble() : (double?)null,
            Confidence = d.Output.TryGetProperty("confidence", out var conf) ? conf.GetDouble() : (double?)null,
            Reasoning = d.Output.TryGetProperty("reasoning", out var reason) ? reason.GetString() : null,
            d.CreatedAt
        }).ToList();

        return Ok(new { items, total, page, pageSize, debug = new { sessionCount, distinctApps, overrideCount, overrideSamples, topAppCategories } });
    }

    [HttpGet("debug-app-candidates")]
    [AllowAnonymous]
    public async Task<IActionResult> DebugAppCandidates(CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-7);

        // Step 1: Get orgs
        var orgs = await _context.ActivitySessions
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(a => a.StartedAt >= cutoff)
            .Select(a => a.OrgId)
            .Distinct()
            .ToListAsync(ct);

        var allSteps = new List<object>();

        foreach (var orgId in orgs.Take(3))
        {
            // Step 2: Get sessions (load first, filter in memory)
            var sessions = await _context.ActivitySessions
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(a => a.OrgId == orgId && a.StartedAt >= cutoff)
                .ToListAsync(ct);

            sessions = sessions.Where(a => !TimeTrack.Backend.Domain.Constants.InternalApps.IsInternal(a.ProcessName)).ToList();

            // Step 3: Group by ProcessName (same as job)
            var appGroups = sessions
                .GroupBy(s => s.ProcessName)
                .Select(g =>
                {
                    var totalSeconds = g.Sum(s => s.DurationSeconds);
                    var totalHours = totalSeconds / 3600.0;
                    var distinctUsers = g.Select(s => s.UserId).Distinct().Count();
                    var currentCategory = g.FirstOrDefault()?.AppCategory ?? "neutral";
                    var perUserHours = g.GroupBy(s => s.UserId)
                        .Select(ug => ug.Sum(s => s.DurationSeconds) / 3600.0)
                        .OrderByDescending(h => h)
                        .ToList();
                    var maxUserHours = perUserHours.FirstOrDefault();

                    return new
                    {
                        ProcessName = g.Key,
                        TotalHours = Math.Round(totalHours, 2),
                        DistinctUsers = distinctUsers,
                        CurrentCategory = currentCategory,
                        SessionCount = g.Count(),
                        MaxUserHours = Math.Round(maxUserHours, 2),
                        PassesThreshold = totalHours >= 1.0 || perUserHours.Any(h => h >= 0.5)
                    };
                })
                .OrderByDescending(a => a.TotalHours)
                .ToList();

            // Step 4: Check pending dedup
            var pendingCount = await _context.AiDecisionLogs
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(d => d.OrgId == orgId && d.DecisionType == "app_usage_suggestion" && !d.WasReviewed)
                .CountAsync(ct);

            // Step 5: Check overrides
            var overrides = await _context.AppCategoryOverrides
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(o => o.OrgId == orgId)
                .Select(o => o.Identifier)
                .ToListAsync(ct);

            var filteredGroups = appGroups.Where(a => a.PassesThreshold).ToList();

            // Step 6: Evaluate each candidate with the SAME logic as the job
            var evaluations = new List<object>();
            var gameProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "cs2", "valorant", "valorant-win64-shipping", "league of legends",
                "fortnite", "minecraft", "steam", "epic games", "battlefield",
                "overwatch", "apex legends", "dota2", "csgo",
            };
            var distractionProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "youtube", "netflix", "twitch", "tiktok", "instagram",
                "facebook", "twitter", "reddit", "9gag",
            };
            var devToolProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "code", "devenv", "idea64", "webstorm64", "rider64", "xcode",
                "cursor", "windsurf", "zed", "neovim", "vim",
                "visual studio code", "vscode", "visual studio",
            };

            foreach (var app in filteredGroups)
            {
                var exeLower = app.ProcessName.ToLowerInvariant();
                var exeNoExt = exeLower.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);

                var isGame = gameProcesses.Any(g => exeNoExt.Contains(g, StringComparison.OrdinalIgnoreCase));
                var isDistraction = distractionProcesses.Any(d => exeNoExt.Contains(d, StringComparison.OrdinalIgnoreCase));
                var isDevTool = devToolProcesses.Any(d => exeNoExt.Contains(d, StringComparison.OrdinalIgnoreCase));
                var isPending = pendingCount > 0;
                var hasOverride = overrides.Any(o => o.Equals(app.ProcessName, StringComparison.OrdinalIgnoreCase));

                var wouldSuggest = false;
                var suggestCategory = "none";
                var reason = "";

                if (isGame && app.CurrentCategory != "distraction")
                {
                    wouldSuggest = true;
                    suggestCategory = "distraction/gaming";
                    reason = $"Game detected: exeNoExt='{exeNoExt}' matches game set, currentCategory='{app.CurrentCategory}' != distraction";
                }
                else if (isDistraction && app.CurrentCategory != "distraction")
                {
                    wouldSuggest = true;
                    suggestCategory = "distraction/entertainment";
                    reason = $"Distraction process: exeNoExt='{exeNoExt}' matches distraction set, currentCategory='{app.CurrentCategory}' != distraction";
                }
                else if (isDevTool && (app.CurrentCategory == "neutral" || app.CurrentCategory == null))
                {
                    wouldSuggest = true;
                    suggestCategory = "productive/development";
                    reason = $"Dev tool: exeNoExt='{exeNoExt}' matches dev set, currentCategory='{app.CurrentCategory}'";
                }
                else
                {
                    reason = $"No heuristic match. isGame={isGame}, isDistraction={isDistraction}, isDevTool={isDevTool}, currentCategory='{app.CurrentCategory}'";
                }

                var blocked = hasOverride ? "BLOCKED: has override" : isPending ? "BLOCKED: pending" : "";

                evaluations.Add(new
                {
                    app.ProcessName,
                    exeNoExt,
                    app.TotalHours,
                    app.CurrentCategory,
                    app.MaxUserHours,
                    isGame,
                    isDistraction,
                    isDevTool,
                    wouldSuggest,
                    suggestCategory,
                    blocked,
                    reason
                });
            }

            allSteps.Add(new
            {
                orgId,
                totalSessions = sessions.Count,
                appGroupCount = appGroups.Count,
                filteredGroupCount = filteredGroups.Count,
                pendingSuggestions = pendingCount,
                overrideCount = overrides.Count,
                overrideNames = overrides,
                evaluations
            });
        }

        return Ok(new { orgCount = orgs.Count, steps = allSteps });
    }

    /// <summary>
    /// Clears all AI module data for the current user (for re-testing).
    /// </summary>
    [HttpDelete("clear-ai-data")]
    public async Task<IActionResult> ClearAiData(CancellationToken ct)
    {
        if (_currentUser.UserId == null || _currentUser.OrgId == null)
            return Forbid();

        var userId = _currentUser.UserId.Value;
        var orgId = _currentUser.OrgId.Value;

        var alerts = await _context.SmartAlerts.IgnoreQueryFilters()
            .Where(a => a.UserId == userId || a.AboutUserId == userId).ToListAsync(ct);
        var patterns = await _context.UserPatterns.IgnoreQueryFilters()
            .Where(p => p.UserId == userId).ToListAsync(ct);
        var anomalies = await _context.BehavioralAnomalies.IgnoreQueryFilters()
            .Where(a => a.UserId == userId).ToListAsync(ct);
        var decisions = await _context.AiDecisionLogs.IgnoreQueryFilters()
            .Where(d => d.UserId == userId).ToListAsync(ct);

        _context.SmartAlerts.RemoveRange(alerts);
        _context.UserPatterns.RemoveRange(patterns);
        _context.BehavioralAnomalies.RemoveRange(anomalies);
        _context.AiDecisionLogs.RemoveRange(decisions);
        await _context.SaveChangesAsync(ct);

        return Ok(new
        {
            message = "AI data cleared",
            removed = new { alerts = alerts.Count, patterns = patterns.Count, anomalies = anomalies.Count, decisions = decisions.Count }
        });
    }
}
