using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Domain.Entities;
using TimeTrack.Agent.Domain.Services;
using TimeTrack.Agent.Domain.ValueObjects;

namespace TimeTrack.Agent.Application.UseCases.ConsolidateSession;

/// <summary>
/// Use Case para consolidar sessões de atividade
/// Responsável por mesclar sessões similares e persistir
/// </summary>
public sealed class ConsolidateSessionUseCase
{
    private readonly IActivitySessionRepository _sessionRepository;
    private readonly ICurrentUserContext _userContext;
    private readonly IIdempotencyKeyGenerator _idempotencyKeyGenerator;
    private readonly ILogger<ConsolidateSessionUseCase> _logger;

    /// <summary>
    /// Tempo máximo de gap entre sessões para considerar mesclagem (em segundos)
    /// </summary>
    private const int MaxMergeGapSeconds = 30;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ConsolidateSessionUseCase(
        IActivitySessionRepository sessionRepository,
        ICurrentUserContext userContext,
        IIdempotencyKeyGenerator idempotencyKeyGenerator,
        ILogger<ConsolidateSessionUseCase> logger)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
        _idempotencyKeyGenerator = idempotencyKeyGenerator ?? throw new ArgumentNullException(nameof(idempotencyKeyGenerator));
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

        var userId = _userContext.UserId
            ?? throw new InvalidOperationException("User not authenticated");

        var session = request.Session;
        var wasMerged = false;

        // Busca sessão mais recente para possível mesclagem (filtrada por usuário)
        var recentSession = await _sessionRepository.GetActiveSessionAsync(userId, cancellationToken);

        if (recentSession != null && CanMerge(recentSession, session))
        {
            // Mescla as sessões
            session = MergeSessions(recentSession, session, userId);
            wasMerged = true;

            _logger.LogDebug(
                "Sessions merged: {RecentId} + {NewId} -> {MergedDuration}",
                recentSession.Id, request.Session.Id, session.Duration);
        }

        // Persiste a sessão com outbox item para garantir sincronização com o backend
        await SaveWithOutboxAsync(session, cancellationToken);

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
    /// Salva a sessão com outbox item em uma única transação
    /// </summary>
    private async Task SaveWithOutboxAsync(ActivitySession session, CancellationToken cancellationToken)
    {
        var payloadJson = JsonSerializer.Serialize(new
        {
            id = session.Id,
            exePathHash = session.App.ExePathHash,
            displayName = session.App.DisplayName,
            categoryProductivity = session.App.Category.Productivity,
            categorySubcategory = session.App.Category.Subcategory,
            categorySource = session.App.Category.Source,
            startUtc = session.Period.StartUtc,
            endUtc = session.Period.EndUtc,
            windowHash = session.WindowHash,
            windowTitle = session.WindowTitle
        }, _jsonOptions);

        var idempotencyKey = _idempotencyKeyGenerator.Generate(
            "activity_session",
            session.Id,
            session.Period.StartUtc);

        var outboxItem = OutboxItem.Create(
            "activity_session",
            session.Id,
            payloadJson,
            idempotencyKey);

        await _sessionRepository.SaveWithOutboxAsync(session, new[] { outboxItem }, cancellationToken);
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
    private static ActivitySession MergeSessions(ActivitySession recent, ActivitySession current, Guid userId)
    {
        // Usa o ID da sessão mais recente
        return ActivitySession.Create(
            userId,
            recent.App,
            new TimeRange(
                recent.Period.StartUtc,
                current.Period.EndUtc),
            recent.WindowHash,
            recent.WindowTitle ?? current.WindowTitle);
    }
}
