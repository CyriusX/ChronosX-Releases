using MediatR;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Application.Auth.DTOs;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Auth.Commands;

/// <summary>
/// Command para obter status da equipe com tempo trabalhado
/// </summary>
public sealed record GetTeamStatusCommand(Guid OrgId, string? Timezone = null) : IRequest<TeamStatusResponse>;

public sealed class GetTeamStatusCommandHandler : IRequestHandler<GetTeamStatusCommand, TeamStatusResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IActivitySessionRepository _sessionRepository;
    private readonly ICurrentUserContext _currentUser;
    private readonly ILogger<GetTeamStatusCommandHandler> _logger;

    public GetTeamStatusCommandHandler(
        IUserRepository userRepository,
        IActivitySessionRepository sessionRepository,
        ICurrentUserContext currentUser,
        ILogger<GetTeamStatusCommandHandler> logger)
    {
        _userRepository = userRepository;
        _sessionRepository = sessionRepository;
        _currentUser = currentUser;
        _logger = logger;
    }

    private static (DateTime UtcStart, DateTime UtcEnd) GetTodayUtcBoundaries(string? timezone)
    {
        if (!string.IsNullOrEmpty(timezone))
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
                var nowInTz = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
                var localToday = nowInTz.Date;
                var localTomorrow = localToday.AddDays(1);
                var utcStart = TimeZoneInfo.ConvertTimeToUtc(
                    DateTime.SpecifyKind(localToday, DateTimeKind.Unspecified), tz);
                var utcEnd = TimeZoneInfo.ConvertTimeToUtc(
                    DateTime.SpecifyKind(localTomorrow, DateTimeKind.Unspecified), tz);
                return (utcStart, utcEnd);
            }
            catch { /* fall through to UTC */ }
        }
        var today = DateTime.UtcNow.Date;
        return (today, today.AddDays(1));
    }

    public async Task<TeamStatusResponse> Handle(GetTeamStatusCommand request, CancellationToken cancellationToken)
    {
        // Verify user belongs to the org
        if (_currentUser.OrgId != request.OrgId)
        {
            throw new ForbiddenException("Access denied to this organization");
        }

        // Get today's date range using caller's timezone for correct day boundaries
        var (today, tomorrow) = GetTodayUtcBoundaries(request.Timezone);

        _logger.LogDebug("GetTeamStatus: Today={Today}, Tomorrow={Tomorrow}", today, tomorrow);

        // Get all users in the org
        var users = await _userRepository.GetByOrgIdAsync(request.OrgId, cancellationToken);

        // Get all activity sessions for the org today
        var sessions = await _sessionRepository.GetByOrgIdAndDateRangeAsync(
            request.OrgId,
            today,
            tomorrow,
            cancellationToken);

        _logger.LogDebug("GetTeamStatus: Found {SessionCount} sessions for org {OrgId}", sessions.Count(), request.OrgId);

        // Filter out internal/system apps to match dashboard totals (shared constant)
        var filteredSessions = sessions
            .Where(s => !Domain.Constants.InternalApps.IsInternal(s.ProcessName))
            .ToList();

        // Group sessions by user and calculate total duration.
        // Clip each session to the day boundaries so cross-midnight sessions only contribute
        // the portion that falls within today — matching GetDailyActivityAggregateAsync behaviour.
        var sessionsByUser = filteredSessions
            .GroupBy(s => s.UserId)
            .ToDictionary(g => g.Key, g => (int)g.Sum(s =>
            {
                var clippedStart = s.StartedAt < today ? today : s.StartedAt;
                var clippedEnd = s.EndedAt > tomorrow ? tomorrow : s.EndedAt;
                return Math.Max(0, (clippedEnd - clippedStart).TotalSeconds);
            }));

        // Find users currently tracking (had activity in last 5 minutes)
        var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-5);
        var currentlyTracking = sessions
            .Where(s => s.EndedAt >= fiveMinutesAgo)
            .Select(s => s.UserId)
            .Distinct()
            .ToHashSet();

        // Build response
        var members = users.Select(u =>
        {
            var totalSeconds = sessionsByUser.GetValueOrDefault(u.Id, 0);
            return new TeamMemberStatusItem
            {
                UserId = u.Id,
                DisplayName = u.DisplayName,
                Role = u.Role.ToString(),
                Status = u.Status.ToString(),
                TodayDurationSeconds = totalSeconds,
                TodayDurationFormatted = FormatDuration(totalSeconds),
                IsTracking = currentlyTracking.Contains(u.Id)
            };
        }).ToList();

        return new TeamStatusResponse
        {
            Members = members,
            TotalCount = members.Count,
            ActiveCount = members.Count(m => m.Status == "Active"),
            TrackingCount = members.Count(m => m.IsTracking)
        };
    }

    private static string FormatDuration(int totalSeconds)
    {
        if (totalSeconds <= 0)
            return "0h 0m";

        var hours = totalSeconds / 3600;
        var minutes = (totalSeconds % 3600) / 60;

        return $"{hours}h {minutes}m";
    }
}
