using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.Integrations.Linear.Services;

/// <summary>
/// Per-user queue of Linear status pushes that failed during <c>MoveTask</c>
/// and need to be retried. Persisted inside
/// <see cref="UserIntegration.MetadataJson"/> alongside the workflow state
/// cache via <see cref="LinearIntegrationMetadata"/>.
///
/// Retry happens opportunistically at the end of every manual
/// <c>SyncFromLinearCommand</c> run — the plan intentionally defers a
/// dedicated Hangfire worker.
/// </summary>
public static class LinearPendingPushQueue
{
    private const int MaxAttempts = 5;

    public static IReadOnlyList<LinearIntegrationMetadata.PendingPushEntry> List(UserIntegration integration)
    {
        var meta = LinearIntegrationMetadata.Load(integration);
        return meta.PendingPushes.Values.ToList();
    }

    public static void Enqueue(UserIntegration integration, Guid taskId, ProjectTaskStatus targetStatus, string? error)
    {
        var meta = LinearIntegrationMetadata.Load(integration);
        var key = taskId.ToString();
        if (!meta.PendingPushes.TryGetValue(key, out var entry))
        {
            entry = new LinearIntegrationMetadata.PendingPushEntry
            {
                TaskId = taskId,
                QueuedAt = DateTime.UtcNow
            };
            meta.PendingPushes[key] = entry;
        }
        entry.TargetStatus = targetStatus;
        entry.AttemptCount += 1;
        entry.LastError = error;
        meta.Save(integration);
    }

    public static bool Dequeue(UserIntegration integration, Guid taskId)
    {
        var meta = LinearIntegrationMetadata.Load(integration);
        if (!meta.PendingPushes.Remove(taskId.ToString())) return false;
        meta.Save(integration);
        return true;
    }

    /// <summary>
    /// Drops entries that have exceeded the max attempt count so a permanently
    /// broken task doesn't keep the queue hot forever.
    /// </summary>
    public static int Prune(UserIntegration integration)
    {
        var meta = LinearIntegrationMetadata.Load(integration);
        var toRemove = meta.PendingPushes
            .Where(kv => kv.Value.AttemptCount >= MaxAttempts)
            .Select(kv => kv.Key)
            .ToList();
        if (toRemove.Count == 0) return 0;
        foreach (var key in toRemove) meta.PendingPushes.Remove(key);
        meta.Save(integration);
        return toRemove.Count;
    }
}
