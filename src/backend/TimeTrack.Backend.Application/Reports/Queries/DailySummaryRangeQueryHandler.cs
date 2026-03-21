using MediatR;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Common.Security;
using TimeTrack.Backend.Application.Reports.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Reports.Queries;

/// <summary>
/// Handler para obter resumo diário de um período (heatmap estilo GitHub)
///
/// SRP: Apenas orquestra a busca de dados de resumo diário
/// DIP: Depende de IReportRepository (abstração)
/// </summary>
public sealed class DailySummaryRangeQueryHandler : IRequestHandler<DailySummaryRangeQuery, DailySummaryRangeResponse>
{
    private readonly IReportRepository _reportRepository;
    private readonly IUserAuthorizationService _authorizationService;
    private readonly ICurrentUserContext _currentUser;

    public DailySummaryRangeQueryHandler(
        IReportRepository reportRepository,
        IUserAuthorizationService authorizationService,
        ICurrentUserContext currentUser)
    {
        _reportRepository = reportRepository;
        _authorizationService = authorizationService;
        _currentUser = currentUser;
    }

    public async Task<DailySummaryRangeResponse> Handle(
        DailySummaryRangeQuery request,
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
        var dailySummaries = await _reportRepository.GetDailySummaryRangeAsync(
            targetUserId,
            request.StartDate,
            request.EndDate,
            cancellationToken);

        // Mapear para response
        return new DailySummaryRangeResponse
        {
            Days = dailySummaries.Select(d => new DailySummaryDayItem
            {
                Date = d.Date.ToString("yyyy-MM-dd"),
                TotalActiveSeconds = d.TotalActiveSeconds,
                TotalIdleSeconds = d.TotalIdleSeconds,
                ProductivityRatio = Math.Round(d.ProductivityRatio, 2)
            }).ToList()
        };
    }
}
