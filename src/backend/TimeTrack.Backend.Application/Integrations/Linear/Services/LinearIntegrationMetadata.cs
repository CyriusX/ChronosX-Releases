using System.Text.Json;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.Integrations.Linear.Services;

/// <summary>
/// Shared read/write contract for <see cref="UserIntegration.MetadataJson"/>.
/// Two concerns live in the same JSON blob — the team workflow state cache
/// and the pending-push retry queue — so they must share one root shape
/// or they'll stomp on each other's slot on write.
/// </summary>
public sealed class LinearIntegrationMetadata
{
    public Dictionary<string, TeamStatesEntry> TeamStates { get; set; } = new();
    public Dictionary<string, PendingPushEntry> PendingPushes { get; set; } = new();

    public sealed class TeamStatesEntry
    {
        public DateTime CachedAt { get; set; }
        public List<CachedWorkflowState> States { get; set; } = new();
    }

    public sealed class CachedWorkflowState
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public double Position { get; set; }
    }

    public sealed class PendingPushEntry
    {
        public Guid TaskId { get; set; }
        public ProjectTaskStatus TargetStatus { get; set; }
        public DateTime QueuedAt { get; set; }
        public int AttemptCount { get; set; }
        public string? LastError { get; set; }
    }

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public static LinearIntegrationMetadata Load(UserIntegration integration)
    {
        if (string.IsNullOrWhiteSpace(integration.MetadataJson))
            return new LinearIntegrationMetadata();
        try
        {
            return JsonSerializer.Deserialize<LinearIntegrationMetadata>(integration.MetadataJson, JsonOptions)
                ?? new LinearIntegrationMetadata();
        }
        catch
        {
            return new LinearIntegrationMetadata();
        }
    }

    public void Save(UserIntegration integration)
    {
        integration.UpdateMetadata(JsonSerializer.Serialize(this, JsonOptions));
    }
}
