using MediatR;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Common.Security;
using TimeTrack.Backend.Application.FocusScore;
using TimeTrack.Backend.Application.Reports.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;

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
    private readonly ILogger<DailySummaryRangeQueryHandler> _logger;

    public DailySummaryRangeQueryHandler(
        IReportRepository reportRepository,
        IUserAuthorizationService authorizationService,
        ICurrentUserContext currentUser,
        ILogger<DailySummaryRangeQueryHandler> logger)
    {
        _reportRepository = reportRepository;
        _authorizationService = authorizationService;
        _currentUser = currentUser;
        _logger = logger;
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

        var dailyList = dailySummaries.ToList();

        // Calcular agregados do período
        var totalActiveSeconds = dailyList.Sum(d => d.TotalActiveSeconds);
        var totalProductiveSeconds = dailyList.Sum(d => d.ProductiveSeconds);
        var totalDistractionCount = dailyList.Sum(d => d.DistractionCount);
        var totalLongFocusBlockCount = dailyList.Sum(d => d.LongFocusBlockCount);

        _logger.LogInformation(
            "DailySummaryRange: {DaysCount} days, TotalActive={TotalActive}s, Productive={Productive}s, Distractions={Distractions}, FocusBlocks={FocusBlocks}",
            dailyList.Count, totalActiveSeconds, totalProductiveSeconds, totalDistractionCount, totalLongFocusBlockCount);

        // Proporção base de produtividade (simples: tempo_produtivo / tempo_total)
        var periodBaseProductivity = totalActiveSeconds > 0
            ? (double)totalProductiveSeconds / totalActiveSeconds
            : 0;

        // Calcular Focus Score agregado do período
        short periodFocusScore = 0;
        if (totalActiveSeconds > 0)
        {
            var input = FocusScoreInput.Create(
                totalTrackedMs: totalActiveSeconds * 1000,
                focusTimeMs: totalProductiveSeconds * 1000,
                distractionMs: dailyList.Sum(d => d.TotalActiveSeconds - d.ProductiveSeconds) * 1000,
                distractionCount: totalDistractionCount,
                pauseCount: 0,
                idleCount: 0,
                longFocusBlockCount: totalLongFocusBlockCount);

            periodFocusScore = FocusScoreCalculator.Calculate(input);

            _logger.LogInformation(
                "FocusScore calculated: BaseProductivity={BaseProductivity:P}, FocusScore={FocusScore}",
                periodBaseProductivity, periodFocusScore);
        }
        else
        {
            _logger.LogWarning("FocusScore not calculated: TotalActiveSeconds is 0");
        }

        // Mapear para response
        return new DailySummaryRangeResponse
        {
            Days = dailyList.Select(d => new DailySummaryDayItem
            {
                Date = d.Date.ToString("yyyy-MM-dd"),
                TotalActiveSeconds = d.TotalActiveSeconds,
                TotalIdleSeconds = d.TotalIdleSeconds,
                ProductivityRatio = Math.Round(d.ProductivityRatio, 2),
                FocusScore = d.FocusScore
            }).ToList(),
            PeriodFocusScore = periodFocusScore,
            PeriodBaseProductivity = Math.Round(periodBaseProductivity, 4)
        };
    }
}
