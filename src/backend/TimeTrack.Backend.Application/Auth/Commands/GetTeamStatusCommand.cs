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
public sealed record GetTeamStatusCommand(Guid OrgId) : IRequest<TeamStatusResponse>;

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

    public async Task<TeamStatusResponse> Handle(GetTeamStatusCommand request, CancellationToken cancellationToken)
    {
        // Verify user belongs to the org
        if (_currentUser.OrgId != request.OrgId)
        {
            throw new ForbiddenException("Access denied to this organization");
        }

        // Get today's date range in UTC (start of today to end of today)
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

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

        // Group sessions by user and calculate total duration
        var sessionsByUser = sessions
            .GroupBy(s => s.UserId)
            .ToDictionary(g => g.Key, g => g.Sum(s => s.DurationSeconds));

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
