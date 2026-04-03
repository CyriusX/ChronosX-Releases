using MediatR;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Common.Security;
using TimeTrack.Backend.Application.Reports.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Reports.Queries;

/// <summary>
/// Handler para obter resumo diário de atividade
/// </summary>
public sealed class DailySummaryQueryHandler : IRequestHandler<DailySummaryQuery, DailySummaryResponse>
{
    private readonly IReportRepository _reportRepository;
    private readonly IUserAuthorizationService _authorizationService;
    private readonly ICurrentUserContext _currentUser;

    public DailySummaryQueryHandler(
        IReportRepository reportRepository,
        IUserAuthorizationService authorizationService,
        ICurrentUserContext currentUser)
    {
        _reportRepository = reportRepository;
        _authorizationService = authorizationService;
        _currentUser = currentUser;
    }

    public async Task<DailySummaryResponse> Handle(
        DailySummaryQuery request,
        CancellationToken cancellationToken)
    {
        // Determinar userId alvo
        var targetUserId = request.UserId ?? _currentUser.UserId!.Value;

        // Validar autorização
        _authorizationService.EnsureCanAccessUserData(targetUserId);

        // Buscar dados sequencialmente — EF Core DbContext is not thread-safe
        var activity = await _reportRepository.GetDailyActivityAggregateAsync(
            targetUserId, request.Date, request.Timezone, cancellationToken);
        var totalIdleSeconds = await _reportRepository.GetDailyIdleSecondsAsync(
            targetUserId, request.Date, request.Timezone, cancellationToken);

        // Mapear para response
        return new DailySummaryResponse
        {
            Date = request.Date.ToString("yyyy-MM-dd"),
            TotalActiveSeconds = activity.TotalSeconds,
            TotalIdleSeconds = totalIdleSeconds,
            FirstActivity = activity.FirstActivity?.ToString("HH:mm:ss"),
            LastActivity = activity.LastActivity?.ToString("HH:mm:ss"),
            Apps = activity.Apps.Select(a => new DailyAppSummary
            {
                ProcessName = a.ProcessName,
                DisplayName = a.DisplayName ?? a.ProcessName,
                TotalSeconds = a.TotalSeconds,
                SessionCount = a.SessionCount,
                AppCategory = a.Subcategory ?? a.Productivity,
                Productivity = a.Productivity
            }).ToList()
        };
    }
}
