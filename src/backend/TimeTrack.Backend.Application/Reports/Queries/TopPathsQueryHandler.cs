using MediatR;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Common.Security;
using TimeTrack.Backend.Application.Reports.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Reports.Queries;

/// <summary>
/// Handler para obter top URLs e caminhos extraídos de window_title
///
/// SRP: Apenas orquestra a busca de top paths
/// DIP: Depende de IReportRepository (abstração)
/// </summary>
public sealed class TopPathsQueryHandler : IRequestHandler<TopPathsQuery, TopPathsResponse>
{
    private readonly IReportRepository _reportRepository;
    private readonly IUserAuthorizationService _authorizationService;
    private readonly ICurrentUserContext _currentUser;

    public TopPathsQueryHandler(
        IReportRepository reportRepository,
        IUserAuthorizationService authorizationService,
        ICurrentUserContext currentUser)
    {
        _reportRepository = reportRepository;
        _authorizationService = authorizationService;
        _currentUser = currentUser;
    }

    public async Task<TopPathsResponse> Handle(
        TopPathsQuery request,
        CancellationToken cancellationToken)
    {
        // Validar período
        if (request.StartDate > request.EndDate)
        {
            throw new ArgumentException("Start date must be before or equal to end date");
        }

        // Validar limite
        var limit = Math.Clamp(request.Limit, 1, 100);

        // Determinar userId(s) alvo
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

        // Buscar dados
        var paths = await _reportRepository.GetTopPathsAsync(
            targetUserIds,
            request.StartDate,
            request.EndDate,
            limit,
            request.Timezone,
            cancellationToken);

        // Mapear para response
        return new TopPathsResponse
        {
            Paths = paths.Select(p => new TopPathResponseItem
            {
                Title = p.Title,
                FilePath = p.FilePath,
                Path = p.Path,
                SourceApp = p.SourceApp,
                TotalSeconds = p.TotalSeconds,
                VisitCount = p.VisitCount
            }).ToList()
        };
    }
}
