using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Domain.Entities;
using TimeTrack.Agent.Domain.ValueObjects;

namespace TimeTrack.Agent.Application.UseCases.ConsolidateSession;

/// <summary>
/// Use Case para consolidar sessões de atividade
/// Responsável por mesclar sessões similares e persistir
/// </summary>
public sealed class ConsolidateSessionUseCase
{
    private readonly IActivitySessionRepository _sessionRepository;
    private readonly ILogger<ConsolidateSessionUseCase> _logger;

    /// <summary>
    /// Tempo máximo de gap entre sessões para considerar mesclagem (em segundos)
    /// </summary>
    private const int MaxMergeGapSeconds = 30;

    public ConsolidateSessionUseCase(
        IActivitySessionRepository sessionRepository,
        ILogger<ConsolidateSessionUseCase> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executa a consolidação de sessão
    /// </summary>
    public async Task<ConsolidateSessionResponse> ExecuteAsync(
        ConsolidateSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Session == null)
            throw new ArgumentNullException(nameof(request), "Session is required");

        var session = request.Session;
        var wasMerged = false;

        // Busca sessão mais recente para possível mesclagem
        var recentSession = await _sessionRepository.GetActiveSessionAsync(cancellationToken);

        if (recentSession != null && CanMerge(recentSession, session))
        {
            // Mescla as sessões
            session = MergeSessions(recentSession, session);
            wasMerged = true;

            _logger.LogDebug(
                "Sessions merged: {RecentId} + {NewId} -> {MergedDuration}",
                recentSession.Id, request.Session.Id, session.Duration);
        }

        // Persiste a sessão
        await _sessionRepository.SaveAsync(session, cancellationToken);

        _logger.LogInformation(
            "Session consolidated: {App} ({Duration:mm\\:ss})",
            session.App.DisplayName, session.Duration);

        return new ConsolidateSessionResponse
        {
            SessionId = session.Id,
            WasMerged = wasMerged,
            Duration = session.Duration
        };
    }

    /// <summary>
    /// Verifica se duas sessões podem ser mescladas
    /// </summary>
    private static bool CanMerge(ActivitySession recent, ActivitySession current)
    {
        // Mesmo aplicativo?
        if (recent.App.ExePathHash != current.App.ExePathHash)
            return false;

        // Gap de tempo aceitável?
        var gap = (current.Period.StartUtc - recent.Period.EndUtc).TotalSeconds;
        if (gap > MaxMergeGapSeconds || gap < 0)
            return false;

        // Mesmo título de janela (se aplicável)?
        if (recent.WindowHash != null && current.WindowHash != null)
        {
            if (recent.WindowHash != current.WindowHash)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Mescla duas sessões em uma
    /// </summary>
    private static ActivitySession MergeSessions(ActivitySession recent, ActivitySession current)
    {
        // Usa o ID da sessão mais recente
        return ActivitySession.Create(
            recent.App,
            new TimeRange(
                recent.Period.StartUtc,
                current.Period.EndUtc),
            recent.WindowHash,
            recent.WindowTitle ?? current.WindowTitle);
    }
}
