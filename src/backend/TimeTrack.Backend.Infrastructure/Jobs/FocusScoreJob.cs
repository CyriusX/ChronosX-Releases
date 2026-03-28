using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Application.FocusScore;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;
using TimeTrack.Backend.Infrastructure.Jobs.Interfaces;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Jobs;

/// <summary>
/// Job Hangfire para cálculo diário do Focus Score
///
/// Responsabilidades:
/// - Orquestra o cálculo do score (não contém lógica de cálculo)
/// - Delega classificação para AppProductivityClassifier
/// - Delega cálculo para FocusScoreCalculator
/// - Persiste via DailyFocusScoreRepository
///
/// SRP: Apenas orquestra o processo de cálculo
/// OCP: Extensível via injeção de dependências
/// </summary>
public sealed class FocusScoreJob : IFocusScoreJob
{
    private readonly TimeTrackDbContext _context;
    private readonly IDailyFocusScoreRepository _focusScoreRepository;
    private readonly AppProductivityClassifier _classifier;
    private readonly ILogger<FocusScoreJob> _logger;

    // Minimum duration for a focus block to be considered "long" (25 minutes in ms)
    private const long LongFocusBlockThresholdMs = 25 * 60 * 1000;

    public FocusScoreJob(
        TimeTrackDbContext context,
        IDailyFocusScoreRepository focusScoreRepository,
        AppProductivityClassifier classifier,
        ILogger<FocusScoreJob> logger)
    {
        _context = context;
        _focusScoreRepository = focusScoreRepository;
        _classifier = classifier;
        _logger = logger;
    }

    // ============================================================================
    // Public API - IFocusScoreJob Implementation
    // ============================================================================

    /// <summary>
    /// Calcula os focus scores para uma data específica
    /// </summary>
    public async Task ExecuteForDateAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var targetDate = DateOnly.FromDateTime(date.Date);
        _logger.LogInformation("Starting Focus Score calculation for date {Date}", targetDate);

        try
        {
            var usersToProcess = await GetUsersWithActivityAsync(targetDate, cancellationToken);

            _logger.LogInformation("Found {Count} users with activity on {Date}",
                usersToProcess.Count, targetDate);

            var processed = 0;
            var errors = 0;

            foreach (var (userId, orgId) in usersToProcess)
            {
                try
                {
                    await CalculateAndSaveScoreAsync(orgId, userId, targetDate, cancellationToken);
                    processed++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error calculating focus score for user {UserId} on {Date}",
                        userId, targetDate);
                    errors++;
                }
            }

            _logger.LogInformation(
                "Focus Score calculation completed for {Date}. Processed: {Processed}, Errors: {Errors}",
                targetDate, processed, errors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing focus score job for date {Date}", targetDate);
            throw;
        }
    }

    /// <summary>
    /// Calcula os focus scores para os últimos N dias
    /// </summary>
    public async Task ExecuteForRecentDaysAsync(int days, CancellationToken cancellationToken = default)
    {
        if (days <= 0)
            throw new ArgumentException("Days must be greater than 0", nameof(days));

        _logger.LogInformation("Starting Focus Score calculation for last {Days} days", days);

        var endDate = DateTime.UtcNow.Date;
        var startDate = endDate.AddDays(-days);

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            await ExecuteForDateAsync(date, cancellationToken);
        }

        _logger.LogInformation("Completed Focus Score calculation for {Days} days range", days);
    }

    /// <summary>
    /// Método para Hangfire (sem parâmetros opcionais)
    /// </summary>
    public Task ExecuteForRecentDaysAsync(int days)
    {
        return ExecuteForRecentDaysAsync(days, CancellationToken.None);
    }

    /// <summary>
    /// Recalcula o score de um usuário específico para uma data
    /// </summary>
    public async Task RecalculateUserScoreAsync(
        Guid userId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found for focus score calculation", userId);
            return;
        }

        await CalculateAndSaveScoreAsync(user.OrgId, userId, date, cancellationToken);
    }

    // ============================================================================
    // Private Methods
    // ============================================================================

    private async Task<List<(Guid UserId, Guid OrgId)>> GetUsersWithActivityAsync(
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var startOfDay = date.ToDateTime(TimeOnly.MinValue);
        var endOfDay = startOfDay.AddDays(1);

        var usersWithActivity = await _context.ActivitySessions
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(a => a.StartedAt >= startOfDay && a.StartedAt < endOfDay)
            .GroupBy(a => new { a.UserId, a.OrgId })
            .Select(g => new { g.Key.UserId, g.Key.OrgId })
            .ToListAsync(cancellationToken);

        return usersWithActivity.ConvertAll(u => (u.UserId, u.OrgId));
    }

    private async Task CalculateAndSaveScoreAsync(
        Guid orgId,
        Guid userId,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var startOfDay = date.ToDateTime(TimeOnly.MinValue);
        var endOfDay = startOfDay.AddDays(1);

        // Get all activity sessions for the day
        var sessions = await _context.ActivitySessions
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(a => a.UserId == userId && a.StartedAt >= startOfDay && a.StartedAt < endOfDay)
            .OrderBy(a => a.StartedAt)
            .ToListAsync(cancellationToken);

        if (sessions.Count == 0)
        {
            _logger.LogDebug("No activity sessions found for user {UserId} on {Date}", userId, date);
            return;
        }

        // Calculate metrics
        var metrics = CalculateMetrics(sessions);

        // Calculate focus score using the domain service
        var input = FocusScoreInput.Create(
            metrics.TotalTrackedMs,
            metrics.FocusTimeMs,
            metrics.DistractionMs,
            metrics.DistractionCount,
            metrics.PauseCount,
            metrics.IdleCount,
            metrics.LongFocusBlockCount);

        var focusScore = FocusScoreCalculator.Calculate(input);

        // Upsert to database
        await _focusScoreRepository.UpsertAsync(
            orgId,
            userId,
            date,
            metrics.TotalTrackedMs,
            metrics.FocusTimeMs,
            metrics.DistractionMs,
            metrics.DistractionCount,
            metrics.PauseCount,
            metrics.IdleCount,
            focusScore,
            deviceId: sessions.FirstOrDefault()?.DeviceId,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Calculated focus score for user {UserId} on {Date}: Score={Score}, FocusTime={FocusTime}ms, Distraction={Distraction}ms",
            userId, date, focusScore, metrics.FocusTimeMs, metrics.DistractionMs);
    }

    private FocusMetrics CalculateMetrics(List<Domain.Entities.ActivitySession> sessions)
    {
        long totalTrackedMs = 0;
        long focusTimeMs = 0;
        long distractionMs = 0;
        int distractionCount = 0;
        int longFocusBlockCount = 0;

        var previousCategory = AppProductivityCategory.Neutral;
        long currentBlockMs = 0;

        foreach (var session in sessions)
        {
            var durationMs = (long)(session.EndedAt - session.StartedAt).TotalMilliseconds;
            totalTrackedMs += durationMs;

            // Classify the app
            var category = _classifier.ClassifyWithContext(
                session.ProcessName,
                session.WindowTitle);

            // Accumulate time by category
            switch (category)
            {
                case AppProductivityCategory.Productive:
                    focusTimeMs += durationMs;
                    currentBlockMs += durationMs;
                    break;

                case AppProductivityCategory.Distraction:
                    distractionMs += durationMs;

                    // Count distraction switches
                    if (previousCategory != AppProductivityCategory.Distraction)
                    {
                        distractionCount++;
                    }

                    // Check if we completed a long focus block
                    if (currentBlockMs >= LongFocusBlockThresholdMs)
                    {
                        longFocusBlockCount++;
                    }
                    currentBlockMs = 0;
                    break;

                default:
                    // Neutral - doesn't count as focus or distraction
                    // Check if we completed a long focus block
                    if (currentBlockMs >= LongFocusBlockThresholdMs)
                    {
                        longFocusBlockCount++;
                    }
                    currentBlockMs = 0;
                    break;
            }

            previousCategory = category;
        }

        // Check final block
        if (currentBlockMs >= LongFocusBlockThresholdMs)
        {
            longFocusBlockCount++;
        }

        // Get pause and idle counts from related data
        // For now, we'll set these to 0 as they need separate tracking
        // TODO: Implement pause counting when pause tracking is available
        var pauseCount = 0;
        var idleCount = 0;

        return new FocusMetrics
        {
            TotalTrackedMs = totalTrackedMs,
            FocusTimeMs = focusTimeMs,
            DistractionMs = distractionMs,
            DistractionCount = distractionCount,
            PauseCount = pauseCount,
            IdleCount = idleCount,
            LongFocusBlockCount = longFocusBlockCount
        };
    }

    private sealed record FocusMetrics
    {
        public long TotalTrackedMs { get; init; }
        public long FocusTimeMs { get; init; }
        public long DistractionMs { get; init; }
        public int DistractionCount { get; init; }
        public int PauseCount { get; init; }
        public int IdleCount { get; init; }
        public int LongFocusBlockCount { get; init; }
    }
}
