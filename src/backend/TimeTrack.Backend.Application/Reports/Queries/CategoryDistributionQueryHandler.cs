using MediatR;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Common.Security;
using TimeTrack.Backend.Application.Reports.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Reports.Queries;

/// <summary>
/// Handler para obter distribuição por categoria de produtividade
///
/// SRP: Apenas orquestra a busca de distribuição por categoria
/// DIP: Depende de IReportRepository (abstração)
/// </summary>
public sealed class CategoryDistributionQueryHandler : IRequestHandler<CategoryDistributionQuery, CategoryDistributionResponse>
{
    private readonly IReportRepository _reportRepository;
    private readonly IUserAuthorizationService _authorizationService;
    private readonly ICurrentUserContext _currentUser;

    public CategoryDistributionQueryHandler(
        IReportRepository reportRepository,
        IUserAuthorizationService authorizationService,
        ICurrentUserContext currentUser)
    {
        _reportRepository = reportRepository;
        _authorizationService = authorizationService;
        _currentUser = currentUser;
    }

    public async Task<CategoryDistributionResponse> Handle(
        CategoryDistributionQuery request,
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
        var categories = await _reportRepository.GetCategoryDistributionAsync(
            targetUserId,
            request.StartDate,
            request.EndDate,
            request.Timezone,
            cancellationToken);

        // Mapear para response
        return new CategoryDistributionResponse
        {
            Categories = categories.Select(c => new CategoryDistributionResponseItem
            {
                Category = c.Category,
                TotalSeconds = c.TotalSeconds,
                Percentage = Math.Round(c.Percentage, 1),
                Subcategories = c.Subcategories.Select(s => new SubcategoryResponseItem
                {
                    Name = s.Name,
                    TotalSeconds = s.TotalSeconds,
                    Percentage = Math.Round(s.Percentage, 1)
                }).ToList()
            }).ToList()
        };
    }
}
