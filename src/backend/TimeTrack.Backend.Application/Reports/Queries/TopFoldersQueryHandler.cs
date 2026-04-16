using MediatR;
using TimeTrack.Backend.Application.Common.Security;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Reports.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Reports.Queries;

public sealed class TopFoldersQueryHandler : IRequestHandler<TopFoldersQuery, TopFoldersResponse>
{
    private readonly IReportRepository _reportRepository;
    private readonly IUserAuthorizationService _authorizationService;
    private readonly ICurrentUserContext _currentUser;

    public TopFoldersQueryHandler(
        IReportRepository reportRepository,
        IUserAuthorizationService authorizationService,
        ICurrentUserContext currentUser)
    {
        _reportRepository = reportRepository;
        _authorizationService = authorizationService;
        _currentUser = currentUser;
    }

    public async Task<TopFoldersResponse> Handle(TopFoldersQuery request, CancellationToken cancellationToken)
    {
        if (request.StartDate > request.EndDate)
            throw new ArgumentException("Start date must be before or equal to end date");

        var limit = Math.Clamp(request.Limit, 1, 100);

        IReadOnlyList<Guid> targetUserIds;
        if (request.UserIds is { Count: > 0 })
        {
            targetUserIds = request.UserIds;
            foreach (var uid in targetUserIds)
                _authorizationService.EnsureCanAccessUserData(uid);
        }
        else
        {
            var targetUserId = request.UserId ?? _currentUser.UserId!.Value;
            _authorizationService.EnsureCanAccessUserData(targetUserId);
            targetUserIds = new[] { targetUserId };
        }

        var folders = await _reportRepository.GetTopFoldersAsync(
            targetUserIds,
            request.StartDate,
            request.EndDate,
            limit,
            request.Timezone,
            cancellationToken);

        return new TopFoldersResponse
        {
            Folders = folders.Select(f => new TopFolderItem
            {
                FolderPath = f.FolderPath,
                TotalSeconds = f.TotalSeconds,
                VisitCount = f.VisitCount
            }).ToList()
        };
    }
}
