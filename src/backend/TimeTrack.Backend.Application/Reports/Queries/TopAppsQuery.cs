using MediatR;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Common.Security;
using TimeTrack.Backend.Application.Reports.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Reports.Queries;

/// <summary>
/// Handler para obter top apps por período
///
/// SRP: Apenas orquestra a busca de top apps
/// DIP: Depende de IReportRepository (abstração)
/// </summary>
public sealed class TopAppsQueryHandler : IRequestHandler<TopAppsQuery, TopAppsResponse>
{
    private readonly IReportRepository _reportRepository;
    private readonly IUserAuthorizationService _authorizationService;
    private readonly ICurrentUserContext _currentUser;

    public TopAppsQueryHandler(
        IReportRepository reportRepository,
        IUserAuthorizationService authorizationService,
        ICurrentUserContext currentUser)
    {
        _reportRepository = reportRepository;
        _authorizationService = authorizationService;
        _currentUser = currentUser;
    }

    public async Task<TopAppsResponse> Handle(
        TopAppsQuery request,
        CancellationToken cancellationToken)
    {
        // Validar período
        if (request.StartDate > request.EndDate)
        {
            throw new ArgumentException("Start date must be before or equal to end date");
        }

        // Validar limite
        var limit = Math.Clamp(request.Limit, 1, 100);

        // Determinar userId alvo
        var targetUserId = request.UserId ?? _currentUser.UserId!.Value;

        // Validar autorização
        _authorizationService.EnsureCanAccessUserData(targetUserId);

        // Buscar dados com filtro de produtividade (se fornecido)
        var apps = await _reportRepository.GetTopAppsAsync(
            targetUserId,
            request.StartDate,
            request.EndDate,
            limit,
            null, // productivityFilter - pode ser adicionado ao DTO depois
            request.Timezone,
            cancellationToken);

        // Mapear para response
        return new TopAppsResponse
        {
            Apps = apps.Select(a => new TopAppItem
            {
                DisplayName = a.DisplayName ?? a.ProcessName,
                TotalSeconds = a.TotalSeconds,
                SessionCount = a.SessionCount,
                Productivity = a.Productivity,
                Subcategory = a.Subcategory
            }).ToList()
        };
    }
}
