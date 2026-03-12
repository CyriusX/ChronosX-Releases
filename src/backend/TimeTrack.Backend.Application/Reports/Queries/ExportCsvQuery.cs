using System.Runtime.CompilerServices;
using MediatR;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Common.Security;
using TimeTrack.Backend.Application.Reports.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Reports.Queries;

/// <summary>
/// Handler para exportar dados de atividade em formato CSV com streaming
/// Agrega os dados de ActivitySession em memória enquanto faz streaming
/// </summary>
public sealed class ExportCsvQueryHandler : IRequestHandler<ExportCsvQuery, IAsyncEnumerable<ExportCsvRow>>
{
    private readonly IActivitySessionRepository _sessionRepository;
    private readonly IUserAuthorizationService _authorizationService;
    private readonly ICurrentUserContext _currentUser;

    public ExportCsvQueryHandler(
        IActivitySessionRepository sessionRepository,
        IUserAuthorizationService authorizationService,
        ICurrentUserContext currentUser)
    {
        _sessionRepository = sessionRepository;
        _authorizationService = authorizationService;
        _currentUser = currentUser;
    }

    public async Task<IAsyncEnumerable<ExportCsvRow>> Handle(
        ExportCsvQuery request,
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

        // Retornar streaming dos dados agregados
        return AggregateSessionsAsync(
            targetUserId,
            request.StartDate,
            request.EndDate,
            cancellationToken);
    }

    /// <summary>
    /// Agrega sessões de atividade agrupando por data e app, mantendo streaming
    /// </summary>
    private async IAsyncEnumerable<ExportCsvRow> AggregateSessionsAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Dicionário para agregar dados em memória enquanto faz streaming
        var aggregatedData = new Dictionary<(DateTime Date, string AppName), (long TotalSeconds, int SessionCount)>();

        // Stream sessions from repository
        await foreach (var session in _sessionRepository.GetSessionsForExportAsync(userId, startDate, endDate, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var key = (session.StartedAt.Date, session.ProcessName);

            if (aggregatedData.TryGetValue(key, out var existing))
            {
                var duration = (session.EndedAt - session.StartedAt).TotalSeconds;
                aggregatedData[key] = (existing.TotalSeconds + (long)duration, existing.SessionCount + 1);
            }
            else
            {
                var duration = (session.EndedAt - session.StartedAt).TotalSeconds;
                aggregatedData[key] = ((long)duration, 1);
            }
        }

        // Yield aggregated results ordered by date
        foreach (var kvp in aggregatedData.OrderBy(x => x.Key.Date).ThenBy(x => x.Key.AppName))
        {
            yield return new ExportCsvRow(
                Data: kvp.Key.Date,
                AppDisplayName: kvp.Key.AppName,
                TempoTotalSegundos: kvp.Value.TotalSeconds,
                SessoesCount: kvp.Value.SessionCount
            );
        }
    }
}
