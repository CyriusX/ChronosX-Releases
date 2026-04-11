using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Application.Integrations.Linear.Services;

/// <summary>
/// Read/write helper for the per-team Linear workflow state cache persisted
/// inside <see cref="UserIntegration.MetadataJson"/>. Reusing the metadata blob
/// avoids a migration and keeps the cache scoped to the integration that owns
/// the credentials that fetched the states.
/// </summary>
public static class LinearTeamStateCache
{
    /// <summary>Workflow states older than this are refetched from Linear.</summary>
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(1);

    /// <summary>
    /// Returns the cached states for <paramref name="teamId"/> if present and fresh,
    /// otherwise null.
    /// </summary>
    public static IReadOnlyList<LinearWorkflowState>? TryGet(
        UserIntegration integration,
        string teamId,
        DateTime now,
        TimeSpan? ttl = null)
    {
        var meta = LinearIntegrationMetadata.Load(integration);
        if (!meta.TeamStates.TryGetValue(teamId, out var entry)) return null;

        var maxAge = ttl ?? DefaultTtl;
        if (now - entry.CachedAt > maxAge) return null;

        return entry.States
            .Select(s => new LinearWorkflowState(s.Id, s.Name, s.Type, s.Position))
            .ToList();
    }

    /// <summary>
    /// Writes the provided states into the integration's metadata blob, merging
    /// with any existing per-team cache entries and preserving the pending-push
    /// queue. Callers must still persist the integration row via the repository.
    /// </summary>
    public static void Store(
        UserIntegration integration,
        string teamId,
        IReadOnlyList<LinearWorkflowState> states,
        DateTime now)
    {
        var meta = LinearIntegrationMetadata.Load(integration);
        meta.TeamStates[teamId] = new LinearIntegrationMetadata.TeamStatesEntry
        {
            CachedAt = now,
            States = states
                .Select(s => new LinearIntegrationMetadata.CachedWorkflowState
                {
                    Id = s.Id,
                    Name = s.Name,
                    Type = s.Type,
                    Position = s.Position
                })
                .ToList()
        };
        meta.Save(integration);
    }
}
