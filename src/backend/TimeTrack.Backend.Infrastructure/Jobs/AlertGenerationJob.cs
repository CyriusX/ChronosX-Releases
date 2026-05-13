using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TimeTrack.Backend.AI.Configuration;
using TimeTrack.Backend.AI.Interfaces;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Jobs;

public sealed class AlertGenerationJob
{
    private readonly TimeTrackDbContext _context;
    private readonly IAIService? _aiService;
    private readonly ILogger<AlertGenerationJob> _logger;
    private readonly AlertGenerationOptions _options;

    public AlertGenerationJob(
        TimeTrackDbContext context,
        IAIService? aiService,
        ILogger<AlertGenerationJob> logger,
        IOptions<AlertGenerationOptions> options)
    {
        _context = context;
        _aiService = aiService;
        _logger = logger;
        _options = options.Value;
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("Starting AlertGenerationJob");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var yesterday = today.AddDays(-1);

        await GenerateOverworkAlertsAsync(yesterday);
        await GenerateFocusDropAlertsAsync(yesterday);
        await GenerateManagerAlertsAsync(yesterday);
        await GenerateTeamContextSwitchingAlertsAsync(yesterday);

        await _context.SaveChangesAsync();

        _logger.LogInformation("AlertGenerationJob completed");
    }

    private async Task GenerateOverworkAlertsAsync(DateOnly targetDate)
    {
        var dayStart = targetDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);

        var scores = await _context.DailyFocusScores
            .IgnoreQueryFilters()
            .Where(d => d.Date == targetDate)
            .ToListAsync();

        var existingSet = await LoadExistingAlertUsersAsync("overwork", dayStart, dayEnd);

        foreach (var score in scores)
        {
            if (existingSet.Contains(score.UserId)) continue;

            var alert = SmartAlert.TryCreateOverworkAlert(score, _options.OverworkThresholdSeconds);
            if (alert == null) continue;

            var aiMessage = await GenerateMessageAsync("overwork", score.OrgId, score.UserId, alert.Message);
            _context.SmartAlerts.Add(SmartAlert.Create(
                score.UserId, score.OrgId, "overwork", aiMessage, "info"));
        }
    }

    private async Task GenerateFocusDropAlertsAsync(DateOnly targetDate)
    {
        var dayStart = targetDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);
        var thirtyDaysAgo = targetDate.AddDays(-30);

        var usersWithBaseline = await _context.DailyFocusScores
            .IgnoreQueryFilters()
            .Where(d => d.Date >= thirtyDaysAgo && d.Date < targetDate)
            .GroupBy(d => d.UserId)
            .Where(g => g.Count() >= 14)
            .Select(g => new { UserId = g.Key, OrgId = g.First().OrgId, AvgFocus = g.Average(s => s.FocusScore) })
            .ToListAsync();

        var yesterdayScores = await _context.DailyFocusScores
            .IgnoreQueryFilters()
            .Where(d => d.Date == targetDate)
            .ToListAsync();

        var existingSet = await LoadExistingAlertUsersAsync("focus_drop", dayStart, dayEnd);

        foreach (var score in yesterdayScores)
        {
            var baseline = usersWithBaseline.FirstOrDefault(b => b.UserId == score.UserId);
            if (baseline == null) continue;
            if (existingSet.Contains(score.UserId)) continue;

            var alert = SmartAlert.TryCreateFocusDropAlert(score, baseline.AvgFocus, _options.FocusDropMultiplier);
            if (alert == null) continue;

            var aiMessage = await GenerateMessageAsync("focus_drop", score.OrgId, score.UserId, alert.Message);
            _context.SmartAlerts.Add(SmartAlert.Create(
                score.UserId, score.OrgId, "focus_drop", aiMessage, "warning",
                actionType: "start_pomodoro"));
        }
    }

    private async Task GenerateManagerAlertsAsync(DateOnly targetDate)
    {
        var dayStart = targetDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);

        var anomalies = await _context.BehavioralAnomalies
            .IgnoreQueryFilters()
            .Where(a => a.AnomalyType == "productivity_drop" && a.DetectedAt == targetDate)
            .ToListAsync();

        var existingAboutIds = await _context.SmartAlerts
            .IgnoreQueryFilters()
            .Where(a => a.AlertType == "team_productivity_drop" && a.CreatedAt >= dayStart && a.CreatedAt < dayEnd)
            .Select(a => a.AboutUserId)
            .ToListAsync();
        var existingAboutSet = new HashSet<Guid>(existingAboutIds.Where(id => id.HasValue).Select(id => id!.Value));

        foreach (var anomaly in anomalies)
        {
            if (existingAboutSet.Contains(anomaly.UserId)) continue;

            var message = await GenerateMessageAsync("team_productivity_drop", anomaly.OrgId, null,
                "Um membro da equipe apresentou queda significativa de produtividade.");

            _context.SmartAlerts.Add(SmartAlert.Create(
                anomaly.UserId, anomaly.OrgId, "team_productivity_drop", message, "warning",
                actionType: "view_report", aboutUserId: anomaly.UserId));
        }
    }

    private async Task GenerateTeamContextSwitchingAlertsAsync(DateOnly targetDate)
    {
        var dayStart = targetDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);
        var sevenDaysAgo = targetDate.AddDays(-7);

        var orgStats = await _context.DailyFocusScores
            .IgnoreQueryFilters()
            .Where(d => d.Date >= sevenDaysAgo && d.Date <= targetDate)
            .GroupBy(d => d.OrgId)
            .Select(g => new
            {
                OrgId = g.Key,
                TotalUsers = g.Select(x => x.UserId).Distinct().Count(),
                HighSwitchingUsers = g.Where(x => x.ContextSwitchesCount > _options.ContextSwitchThreshold)
                    .Select(x => x.UserId).Distinct().Count()
            })
            .ToListAsync();

        var existingOrgIds = await _context.SmartAlerts
            .IgnoreQueryFilters()
            .Where(a => a.AlertType == "team_high_switching" && a.CreatedAt >= dayStart && a.CreatedAt < dayEnd)
            .Select(a => a.OrgId)
            .ToListAsync();
        var existingOrgSet = new HashSet<Guid>(existingOrgIds);

        foreach (var stat in orgStats)
        {
            if (stat.TotalUsers < _options.MinimumTeamSize) continue;
            var ratio = (double)stat.HighSwitchingUsers / stat.TotalUsers;
            if (ratio <= _options.TeamHighSwitchingRatio) continue;
            if (existingOrgSet.Contains(stat.OrgId)) continue;

            var message = $"{Math.Round(ratio * 100)}% da equipe esta com alta alternancia de contexto esta semana.";

            _context.SmartAlerts.Add(SmartAlert.Create(
                Guid.Empty, stat.OrgId, "team_high_switching", message, "info",
                actionType: "view_report"));
        }
    }

    private async Task<HashSet<Guid>> LoadExistingAlertUsersAsync(string alertType, DateTime dayStart, DateTime dayEnd)
    {
        var userIds = await _context.SmartAlerts
            .IgnoreQueryFilters()
            .Where(a => a.AlertType == alertType && a.CreatedAt >= dayStart && a.CreatedAt < dayEnd)
            .Select(a => a.UserId)
            .ToListAsync();
        return new HashSet<Guid>(userIds);
    }

    private async Task<string> GenerateMessageAsync(
        string alertType, Guid orgId, Guid? userId, string fallbackMessage)
    {
        if (_aiService == null) return fallbackMessage;

        try
        {
            var context = new AlertContext
            {
                AlertType = alertType,
                Severity = alertType == "focus_drop" ? "warning" : "info",
                Language = "pt-BR",
            };
            return await _aiService.GenerateAlertMessageAsync(context, orgId, userId);
        }
        catch
        {
            return fallbackMessage;
        }
    }
}
