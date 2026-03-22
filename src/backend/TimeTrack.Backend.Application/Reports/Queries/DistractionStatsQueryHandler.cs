using MediatR;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Common.Security;
using TimeTrack.Backend.Application.Reports.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Reports.Queries;

/// <summary>
/// Handler para obter estatísticas de distração
///
/// SRP: Apenas orquestra a busca de estatísticas de distração
/// DIP: Depende de IReportRepository (abstração)
/// </summary>
public sealed class DistractionStatsQueryHandler : IRequestHandler<DistractionStatsQuery, DistractionStatsResponse>
{
    private readonly IReportRepository _reportRepository;
    private readonly IUserAuthorizationService _authorizationService;
    private readonly ICurrentUserContext _currentUser;

    public DistractionStatsQueryHandler(
        IReportRepository reportRepository,
        IUserAuthorizationService authorizationService,
        ICurrentUserContext currentUser)
    {
        _reportRepository = reportRepository;
        _authorizationService = authorizationService;
        _currentUser = currentUser;
    }

    public async Task<DistractionStatsResponse> Handle(
        DistractionStatsQuery request,
        CancellationToken cancellationToken)
    {
        // Validar período
        if (request.StartDate > request.EndDate)
        {
            throw new ArgumentException("Start date must be before or equal to end date");
        }

        // Determinar userId alvo
        var targetUserId = request.UserId ?? _currentUser.UserId!.Value;

        // Validar autorização
        _authorizationService.EnsureCanAccessUserData(targetUserId);

        // Buscar dados
        var stats = await _reportRepository.GetDistractionStatsAsync(
            targetUserId,
            request.StartDate,
            request.EndDate,
            cancellationToken);

        // Mapear para response
        return new DistractionStatsResponse
        {
            DailyDistractions = stats.DailyDistractions.Select(d => new DailyDistractionItem
            {
                Date = d.Date.ToString("yyyy-MM-dd"),
                DistractionSeconds = d.DistractionSeconds
            }).ToList(),
            TopDistractions = stats.TopDistractions.Select(d => new TopDistractionItem
            {
                DisplayName = d.DisplayName ?? d.ProcessName,
                ProcessName = d.ProcessName,
                TotalSeconds = d.TotalSeconds,
                SessionCount = d.SessionCount,
                Subcategory = d.Subcategory
            }).ToList()
        };
    }
}
