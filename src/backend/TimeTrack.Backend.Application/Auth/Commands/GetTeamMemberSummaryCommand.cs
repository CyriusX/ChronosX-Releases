using MediatR;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Application.Auth.DTOs;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Auth.Commands;

/// <summary>
/// Command para obter resumo de hoje de um membro da equipe
/// </summary>
public sealed record GetTeamMemberSummaryCommand(Guid OrgId, Guid TargetUserId, string? Timezone = null) : IRequest<TeamMemberSummaryResponse>;

public sealed class GetTeamMemberSummaryCommandHandler : IRequestHandler<GetTeamMemberSummaryCommand, TeamMemberSummaryResponse>
{
    // Internal/system apps excluded from dashboard totals (same as agent's local dashboard)
    private static readonly HashSet<string> _internalApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "TimeTrack.DesktopHost",
        "Microsoft Edge WebView2",
        "Microsoft® Windows® Operating System",
        "Sistema operacional Microsoft® Windows®"
    };

    private readonly IUserRepository _userRepository;
    private readonly IReportRepository _reportRepository;
    private readonly ICurrentUserContext _currentUser;
    private readonly ILogger<GetTeamMemberSummaryCommandHandler> _logger;

    public GetTeamMemberSummaryCommandHandler(
        IUserRepository userRepository,
        IReportRepository reportRepository,
        ICurrentUserContext currentUser,
        ILogger<GetTeamMemberSummaryCommandHandler> logger)
    {
        _userRepository = userRepository;
        _reportRepository = reportRepository;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<TeamMemberSummaryResponse> Handle(GetTeamMemberSummaryCommand request, CancellationToken cancellationToken)
    {
        // Verify caller belongs to the org
        if (_currentUser.OrgId != request.OrgId)
            throw new ForbiddenException("Access denied to this organization");

        // Verify target user exists and belongs to the same org
        var targetUser = await _userRepository.GetByIdAsync(request.TargetUserId, cancellationToken);
        if (targetUser == null || targetUser.OrgId != request.OrgId)
            throw new NotFoundException("User", request.TargetUserId);

        // Use caller's timezone to determine "today" correctly
        var today = GetLocalToday(request.Timezone);

        // Fetch sequentially — EF Core DbContext is not thread-safe
        var activity = await _reportRepository.GetDailyActivityAggregateAsync(request.TargetUserId, today, request.Timezone, cancellationToken);
        var totalIdleSeconds = await _reportRepository.GetDailyIdleSecondsAsync(request.TargetUserId, today, request.Timezone, cancellationToken);
        var weeklyHistory = await BuildWeeklyHistoryAsync(request.TargetUserId, today, request.Timezone, cancellationToken);

        // Filter out internal apps and recalculate total
        var filteredApps = activity.Apps
            .Where(a => !_internalApps.Contains(a.ProcessName))
            .ToList();
        var totalSeconds = filteredApps.Sum(a => a.TotalSeconds);

        // Merge aggregates that have the same ProcessName but different category rows
        // (can happen if category changed mid-day). Take the dominant category per app.
        var appsByName = filteredApps
            .GroupBy(a => a.ProcessName)
            .Select(g =>
            {
                var totalSecs = g.Sum(a => a.TotalSeconds);
                var totalSessions = g.Sum(a => a.SessionCount);
                // Pick the category with the most time
                var dominant = g.OrderByDescending(a => a.TotalSeconds).First();
                return new MemberAppSummary
                {
                    Name = dominant.DisplayName ?? g.Key,
                    Duration = totalSecs,
                    Percentage = totalSeconds > 0 ? Math.Round((double)totalSecs / totalSeconds * 100, 1) : 0,
                    Productivity = dominant.Productivity ?? "neutral",
                };
            })
            .OrderByDescending(a => a.Duration)
            .ToList();

        // Calculate productive time
        long productiveSeconds = appsByName
            .Where(a => a.Productivity == "productive")
            .Sum(a => a.Duration);

        // Build categories grouped by productivity level (same as agent's IPC handler)
        var categories = appsByName
            .GroupBy(a => a.Productivity ?? "neutral")
            .Select(g => new MemberCategorySummary
            {
                Name = FormatCategoryName(g.Key),
                Duration = g.Sum(a => a.Duration),
                Percentage = totalSeconds > 0 ? Math.Round(g.Sum(a => a.Duration) / (double)totalSeconds * 100, 1) : 0,
                Color = GetCategoryColor(g.Key),
                Productivity = g.Key
            })
            .OrderByDescending(c => c.Duration)
            .ToList();

        var sessionsCount = activity.Apps.Sum(a => a.SessionCount);

        return new TeamMemberSummaryResponse
        {
            TotalDuration = totalSeconds,
            ProductiveTime = productiveSeconds,
            IdleTime = totalIdleSeconds,
            FocusTime = 0,
            FocusScore = 0,
            SessionsCount = sessionsCount,
            TopProjects = [],
            TopApplications = appsByName.Take(10).ToList(),
            TopAppsByExe = appsByName.Take(10).ToList(),
            Categories = categories,
            WeeklyHistory = weeklyHistory,
            LastSyncAt = activity.LastActivity?.ToString("o") // ISO 8601 UTC
        };
    }

    /// <summary>
    /// Determines "today" in the caller's timezone. Returns a date-only DateTime.
    /// </summary>
    private static DateTime GetLocalToday(string? timezone)
    {
        if (!string.IsNullOrEmpty(timezone))
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).Date;
            }
            catch { /* fall through */ }
        }
        return DateTime.UtcNow.Date;
    }

    private async Task<List<MemberWeeklyHistoryItem>> BuildWeeklyHistoryAsync(
        Guid userId, DateTime today, string? timezone, CancellationToken cancellationToken)
    {
        var dayNames = new[] { "Dom", "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb" };

        // Query each day from real session data (with internal app filter applied)
        // to match the same totals shown in the main dashboard cards
        var history = new List<MemberWeeklyHistoryItem>();
        for (int i = 6; i >= 0; i--)
        {
            var date = today.AddDays(-i);
            double hours = 0;

            try
            {
                var dayActivity = await _reportRepository.GetDailyActivityAggregateAsync(userId, date, timezone, cancellationToken);
                var filteredSeconds = dayActivity.Apps
                    .Where(a => !_internalApps.Contains(a.ProcessName))
                    .Sum(a => a.TotalSeconds);
                hours = filteredSeconds / 3600.0;
            }
            catch
            {
                // If query fails for a day, show 0
            }

            history.Add(new MemberWeeklyHistoryItem
            {
                Date = date.ToString("yyyy-MM-dd"),
                DayName = dayNames[(int)date.DayOfWeek],
                Hours = Math.Round(hours, 2),
                IsToday = i == 0
            });
        }

        return history;
    }

    private static string FormatCategoryName(string key)
    {
        if (string.IsNullOrEmpty(key)) return "Outros";

        var friendlyNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["productive"]  = "Productive",
            ["neutral"]     = "Neutral",
            ["distraction"] = "Distraction",
        };

        if (friendlyNames.TryGetValue(key, out var friendly))
            return friendly;

        return string.Join(' ', key.Split('_').Select(w =>
            w.Length > 0 ? char.ToUpper(w[0]) + w[1..] : w));
    }

    private static string GetCategoryColor(string key) =>
        key?.ToLowerInvariant() switch
        {
            "productive" => "#4ade80",
            "neutral"    => "#fbbf24",
            "distraction" => "#ef4444",
            _            => "#94a3b8"
        };
}
