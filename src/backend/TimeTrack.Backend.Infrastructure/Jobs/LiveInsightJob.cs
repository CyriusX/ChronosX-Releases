using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TimeTrack.Backend.AI.Configuration;
using TimeTrack.Backend.AI.Interfaces;
using TimeTrack.Backend.Application.FocusScore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Services;
using TimeTrack.Backend.Domain.ValueObjects;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Jobs;

public sealed class LiveInsightJob
{
    private readonly TimeTrackDbContext _context;
    private readonly AppProductivityClassifier _classifier;
    private readonly IAIService? _aiService;
    private readonly LiveInsightOptions _options;
    private readonly ILogger<LiveInsightJob> _logger;
    private readonly LiveInsightGenerator _generator = new();

    public LiveInsightJob(
        TimeTrackDbContext context,
        AppProductivityClassifier classifier,
        IOptions<LiveInsightOptions> options,
        IAIService? aiService,
        ILogger<LiveInsightJob> logger)
    {
        _context = context;
        _classifier = classifier;
        _options = options.Value;
        _aiService = aiService;
        _logger = logger;
    }

    public Task ExecuteAsync() => ExecuteAsync(CancellationToken.None);

    public async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting LiveInsightJob");

        var todayStartUtc = DateTime.UtcNow.Date;

        var userSessions = await _context.ActivitySessions
            .IgnoreQueryFilters()
            .Where(s => s.StartedAt >= todayStartUtc)
            .GroupBy(s => new { s.UserId, s.OrgId })
            .Select(g => new
            {
                g.Key.UserId,
                g.Key.OrgId,
                Sessions = g.ToList()
            })
            .ToListAsync(ct);

        var aiCallBudget = _options.MaxAiCallsPerCycle;
        var userMetrics = new List<(Guid UserId, Guid OrgId, LiveMetrics Metrics)>();

        foreach (var user in userSessions)
        {
            var metrics = _generator.ComputeMetrics(user.Sessions);
            userMetrics.Add((user.UserId, user.OrgId, metrics));
        }

        var thresholds = new LiveInsightThresholds
        {
            DistractionThreshold = _options.DistractionThreshold,
            OverworkMinutesThreshold = _options.OverworkMinutesThreshold,
            DeepFocusMinutesThreshold = _options.DeepFocusMinutesThreshold,
        };

        var allCandidates = new List<LiveInsightCandidate>();
        foreach (var (userId, orgId, metrics) in userMetrics)
        {
            allCandidates.AddRange(_generator.DetectConditions(userId, orgId, metrics, thresholds));
        }

        var sorted = allCandidates.OrderByDescending(c => c.Priority).ToList();

        foreach (var candidate in sorted)
        {
            var dedupWindow = DateTime.UtcNow.AddHours(-_options.DeduplicationWindowHours);
            var exists = await _context.SmartAlerts
                .IgnoreQueryFilters()
                .AnyAsync(a => a.UserId == candidate.UserId
                    && a.AlertType == candidate.AlertType
                    && a.CreatedAt >= dedupWindow, ct);

            if (exists) continue;

            string message;
            if (aiCallBudget > 0 && _aiService != null)
            {
                var metrics = userMetrics.First(m => m.UserId == candidate.UserId).Metrics;
                message = await GenerateAiMessageAsync(
                    candidate.AlertType, candidate.OrgId, candidate.UserId, candidate.Message, metrics);
                aiCallBudget--;
            }
            else
            {
                message = candidate.Message;
            }

            _context.SmartAlerts.Add(SmartAlert.Create(
                candidate.UserId, candidate.OrgId, candidate.AlertType, message, candidate.Severity));

            await DispatchInsightNotificationAsync(
                candidate.UserId, candidate.OrgId, candidate.AlertType, message, ct);
        }

        await GenerateTeamAlertsAsync(userMetrics, ct);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("LiveInsightJob completed. Evaluated {Users} users", userMetrics.Count);
    }

    private async Task GenerateTeamAlertsAsync(
        List<(Guid UserId, Guid OrgId, LiveMetrics Metrics)> userMetrics,
        CancellationToken ct)
    {
        var dedupWindow = DateTime.UtcNow.AddHours(-_options.DeduplicationWindowHours);
        var orgGroups = userMetrics.GroupBy(u => u.OrgId);

        foreach (var org in orgGroups)
        {
            var totalUsers = org.Count();
            if (totalUsers < 2) continue;

            var highDistraction = org.Count(u =>
                u.Metrics.TotalMs > 0 &&
                (double)u.Metrics.DistractionMs / u.Metrics.TotalMs > _options.DistractionThreshold);

            var ratio = (double)highDistraction / totalUsers;
            if (ratio <= _options.TeamDistractionRatio) continue;

            var exists = await _context.SmartAlerts
                .IgnoreQueryFilters()
                .AnyAsync(a => a.OrgId == org.Key
                    && a.AlertType == "live_team_distraction"
                    && a.CreatedAt >= dedupWindow, ct);

            if (exists) continue;

            var message = $"{Math.Round(ratio * 100)}% da equipe esta com alta taxa de distracao agora.";

            _context.SmartAlerts.Add(SmartAlert.Create(
                Guid.Empty, org.Key, "live_team_distraction", message, "info",
                actionType: "view_report"));
        }
    }

    private async Task<string> GenerateAiMessageAsync(
        string alertType, Guid orgId, Guid userId, string fallback, LiveMetrics metrics)
    {
        if (_aiService == null) return fallback;

        try
        {
            var data = new Dictionary<string, object>
            {
                ["total_active_minutes"] = Math.Round(metrics.TotalMs / 60_000.0, 1),
                ["productive_minutes"] = Math.Round(metrics.ProductiveMs / 60_000.0, 1),
                ["distraction_minutes"] = Math.Round(metrics.DistractionMs / 60_000.0, 1),
                ["context_switches_last_hour"] = metrics.RecentContextSwitches,
                ["longest_focus_block_minutes"] = Math.Round(metrics.LongestFocusBlockMs / 60_000.0, 1),
                ["top_distraction_app"] = metrics.TopDistractionApp ?? "N/A",
                ["top_distraction_minutes"] = Math.Round(metrics.TopDistractionMs / 60_000.0, 1),
            };

            if (metrics.TotalMs > 0)
            {
                data["distraction_ratio_pct"] = Math.Round((double)metrics.DistractionMs / metrics.TotalMs * 100, 1);
                data["productive_ratio_pct"] = Math.Round((double)metrics.ProductiveMs / metrics.TotalMs * 100, 1);
            }

            var context = new AlertContext
            {
                AlertType = alertType,
                Severity = alertType.Contains("warning") ? "warning" : "info",
                Language = "pt-BR",
                Data = data,
            };
            return await _aiService.GenerateAlertMessageAsync(context, orgId, userId);
        }
        catch
        {
            return fallback;
        }
    }

    private async Task DispatchInsightNotificationAsync(
        Guid userId, Guid orgId, string alertType, string message, CancellationToken ct)
    {
        try
        {
            var notification = AgentNotificationInbox.Create(
                orgId, userId, AgentNotificationKind.AiInsight, "Alerta", message);

            _context.AgentNotificationInbox.Add(notification);

            var devices = await _context.Devices
                .IgnoreQueryFilters()
                .Where(d => d.UserId == userId && d.Status == DeviceStatus.Active)
                .ToListAsync(ct);

            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                notificationId = notification.Id,
                payload = new { alertType, message }
            });

            foreach (var device in devices)
            {
                _context.RemoteCommands.Add(RemoteCommand.Create(
                    orgId, device.Id, "ai_insight", payload, userId, TimeSpan.FromHours(2)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to dispatch insight notification for user {UserId}", userId);
        }
    }
}
