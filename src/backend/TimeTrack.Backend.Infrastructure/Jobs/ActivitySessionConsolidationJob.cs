using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Infrastructure.Jobs.Interfaces;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Jobs;

/// <summary>
/// Job para consolidar sessões de atividade duplicadas no banco de dados
/// Após a sincronização com o agent, as sessões podem ter fragmentadas
/// devido a race conditions durante a captura
/// </summary>
public sealed class ActivitySessionConsolidationJob : IActivitySessionConsolidationJob
{
    private readonly TimeTrackDbContext _context;
    private readonly ILogger<ActivitySessionConsolidationJob> _logger;

    // Gap tolerance for merging sessions (in seconds)
    // Sessions with gaps smaller than this will be merged
    private const int MergeGapToleranceSeconds = 10;

    public ActivitySessionConsolidationJob(
        TimeTrackDbContext context,
        ILogger<ActivitySessionConsolidationJob> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Método para Hangfire - sem parâmetros opcionais
    /// Processa as últimas 12 horas por padrão
    /// </summary>
    public Task ExecuteConsolidationAsync()
    {
        return ExecuteAsync(TimeSpan.FromHours(12), CancellationToken.None);
    }

    /// <summary>
    /// Executa a consolidação de sessões duplicadas para um período específico
    /// </summary>
    public async Task ExecuteAsync(TimeSpan period, CancellationToken cancellationToken = default)
    {
        if (period <= TimeSpan.Zero)
            throw new ArgumentException("Period must be positive", nameof(period));

        _logger.LogInformation("Starting activity session consolidation for period {Period}", period);

        try
        {
            var cutoffDate = DateTime.UtcNow - period;

            // Get all users with activity in the period
            var usersWithActivity = await _context.ActivitySessions
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(s => s.StartedAt >= cutoffDate)
                .Select(s => new { s.UserId, s.OrgId })
                .Distinct()
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Found {Count} users with activity to consolidate",
                usersWithActivity.Count);

            var totalMerged = 0;
            var totalDeleted = 0;

            foreach (var user in usersWithActivity)
            {
                try
                {
                    var (merged, deleted) = await ConsolidateUserSessionsAsync(
                        user.UserId, user.OrgId, cutoffDate, cancellationToken);
                    totalMerged += merged;
                    totalDeleted += deleted;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error consolidating sessions for user {UserId}", user.UserId);
                }
            }

            _logger.LogInformation(
                "Activity session consolidation completed. Merged: {Merged}, Deleted: {Deleted}",
                totalMerged, totalDeleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing activity session consolidation job");
            throw;
        }
    }

    /// <summary>
    /// Executa a consolidação para um usuário específico
    /// </summary>
    public async Task ExecuteForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting activity session consolidation for user {UserId}", userId);

        try
        {
            // Get user's organization first
            var user = await _context.Users
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found for consolidation", userId);
                return;
            }

            var cutoffDate = DateTime.UtcNow.AddDays(-7); // Last 7 days
            var (merged, deleted) = await ConsolidateUserSessionsAsync(userId, user.OrgId, cutoffDate, cancellationToken);

            _logger.LogInformation(
                "Activity session consolidation completed for user {UserId}. Merged: {Merged}, Deleted: {Deleted}",
                userId, merged, deleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error consolidating sessions for user {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// Consolida sessões de um usuário específico
    /// </summary>
    private async Task<(int Merged, int Deleted)> ConsolidateUserSessionsAsync(
        Guid userId,
        Guid orgId,
        DateTime cutoffDate,
        CancellationToken cancellationToken)
    {
        // Get all sessions for the user in the period, ordered by start time
        var sessions = await _context.ActivitySessions
            .IgnoreQueryFilters()
            .Where(s => s.UserId == userId && s.StartedAt >= cutoffDate)
            .OrderBy(s => s.StartedAt)
            .ToListAsync(cancellationToken);

        if (sessions.Count == 0)
            return (0, 0);

        _logger.LogDebug("Processing {Count} sessions for user {UserId}", sessions.Count, userId);

        var mergedCount = 0;
        var deletedCount = 0;
        var idsToDelete = new List<Guid>();

        // Group sessions by app identity (process_name + window_title)
        var sessionGroups = sessions
            .GroupBy(s => new { s.ProcessName, WindowTitle = s.WindowTitle ?? "" })
            .ToList();

        foreach (var group in sessionGroups)
        {
            var groupSessions = group.OrderBy(s => s.StartedAt).ToList();

            if (groupSessions.Count <= 1)
                continue;

            // Find consecutive sessions that can be merged
            for (int i = 1; i < groupSessions.Count; i++)
            {
                var previous = groupSessions[i - 1];
                var current = groupSessions[i];

                // Check if sessions can be merged (gap is within tolerance)
                var gap = (current.StartedAt - previous.EndedAt).TotalSeconds;

                if (gap >= 0 && gap <= MergeGapToleranceSeconds)
                {
                    // Extend previous session to include current using the domain method
                    previous.Extend(current.EndedAt);
                    idsToDelete.Add(current.Id);
                    mergedCount++;

                    _logger.LogDebug(
                        "Merging session {CurrentId} into {PreviousId}. Gap: {Gap}s",
                        current.Id, previous.Id, gap);
                }
            }
        }

        // Delete merged sessions
        if (idsToDelete.Any())
        {
            _context.ActivitySessions.RemoveRange(
                sessions.Where(s => idsToDelete.Contains(s.Id)));

            deletedCount = idsToDelete.Count;

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Deleted {Count} duplicate sessions for user {UserId}", deletedCount, userId);
        }

        return (mergedCount, deletedCount);
    }
}
