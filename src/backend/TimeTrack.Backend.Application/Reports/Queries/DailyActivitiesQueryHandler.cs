using MediatR;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Common.Security;
using TimeTrack.Backend.Application.Reports.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Reports.Queries;

/// <summary>
/// Handler to get individual activity sessions for a specific date.
/// Used by the agent to display timeline blocks for past days.
/// </summary>
public sealed class DailyActivitiesQueryHandler : IRequestHandler<DailyActivitiesQuery, DailyActivitiesResponse>
{
    private readonly IActivitySessionRepository _sessionRepository;
    private readonly IUserAuthorizationService _authorizationService;
    private readonly ICurrentUserContext _currentUser;

    public DailyActivitiesQueryHandler(
        IActivitySessionRepository sessionRepository,
        IUserAuthorizationService authorizationService,
        ICurrentUserContext currentUser)
    {
        _sessionRepository = sessionRepository;
        _authorizationService = authorizationService;
        _currentUser = currentUser;
    }

    public async Task<DailyActivitiesResponse> Handle(
        DailyActivitiesQuery request,
        CancellationToken cancellationToken)
    {
        var targetUserId = request.UserId ?? _currentUser.UserId!.Value;

        _authorizationService.EnsureCanAccessUserData(targetUserId);

        var startOfDay = DateTime.SpecifyKind(request.Date.Date, DateTimeKind.Utc);
        var endOfDay = DateTime.SpecifyKind(startOfDay.AddDays(1), DateTimeKind.Utc);

        var sessions = await _sessionRepository.GetByUserIdAndDateRangeAsync(
            targetUserId, startOfDay, endOfDay, cancellationToken);

        var sessionDtos = sessions
            .OrderBy(s => s.StartedAt)
            .Select(s => new ActivitySessionDto
            {
                ProcessName = s.ProcessName,
                WindowTitle = s.WindowTitle,
                AppCategory = s.AppCategory,
                StartedAt = s.StartedAt,
                EndedAt = s.EndedAt,
                DurationSeconds = s.DurationSeconds,
            })
            .ToList();

        return new DailyActivitiesResponse
        {
            Date = request.Date.ToString("yyyy-MM-dd"),
            Sessions = sessionDtos,
        };
    }
}
