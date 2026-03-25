using MediatR;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Common.Security;
using TimeTrack.Backend.Application.Reports.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Reports.Queries;

/// <summary>
/// Handler para obter tendência de produtividade (gráfico de barras empilhadas)
///
/// SRP: Apenas orquestra a busca de tendência de produtividade
/// DIP: Depende de IReportRepository (abstração)
/// </summary>
public sealed class ProductivityTrendQueryHandler : IRequestHandler<ProductivityTrendQuery, ProductivityTrendResponse>
{
    private readonly IReportRepository _reportRepository;
    private readonly IUserAuthorizationService _authorizationService;
    private readonly ICurrentUserContext _currentUser;

    public ProductivityTrendQueryHandler(
        IReportRepository reportRepository,
        IUserAuthorizationService authorizationService,
        ICurrentUserContext currentUser)
    {
        _reportRepository = reportRepository;
        _authorizationService = authorizationService;
        _currentUser = currentUser;
    }

    public async Task<ProductivityTrendResponse> Handle(
        ProductivityTrendQuery request,
        CancellationToken cancellationToken)
    {
        // Validar período
        if (request.StartDate > request.EndDate)
        {
            throw new ArgumentException("Start date must be before or equal to end date");
        }

        // Validar groupBy
        var groupBy = request.GroupBy.ToLowerInvariant();
        if (groupBy != "day" && groupBy != "week" && groupBy != "month")
        {
            throw new ArgumentException("GroupBy must be 'day', 'week', or 'month'");
        }

        // Determinar userId alvo
        var targetUserId = request.UserId ?? _currentUser.UserId!.Value;

        // Validar autorização
        _authorizationService.EnsureCanAccessUserData(targetUserId);

        // Buscar dados
        var trendItems = await _reportRepository.GetProductivityTrendAsync(
            targetUserId,
            request.StartDate,
            request.EndDate,
            groupBy,
            request.Timezone,
            cancellationToken);

        // Mapear para response
        return new ProductivityTrendResponse
        {
            Periods = trendItems.Select(t => new ProductivityTrendPeriodItem
            {
                Period = t.Period,
                ProductiveSeconds = t.ProductiveSeconds,
                NeutralSeconds = t.NeutralSeconds,
                DistractionSeconds = t.DistractionSeconds,
                IdleSeconds = t.IdleSeconds
            }).ToList()
        };
    }
}
