namespace TimeTrack.Backend.Application.Integrations.DTOs;

public sealed class UserIntegrationResponse
{
    public Guid Id { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string ExternalUserId { get; set; } = string.Empty;
    public string? ExternalUserName { get; set; }
    public string? ExternalUserEmail { get; set; }
    public DateTime ConnectedAt { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

public sealed class ConnectLinearRequest
{
    public string ApiKey { get; set; } = string.Empty;
}

public sealed class LinearSyncResultResponse
{
    public int ProjectsCreated { get; set; }
    public int ProjectsUpdated { get; set; }
    public int TasksCreated { get; set; }
    public int TasksUpdated { get; set; }
    public int TasksSoftDeleted { get; set; }
    public long DurationMs { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime FinishedAt { get; set; }
    public UserIntegrationResponse Integration { get; set; } = new();
}

public sealed class LinearSyncHistoryEntryResponse
{
    public Guid Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime FinishedAt { get; set; }
    public long DurationMs { get; set; }
    public int ProjectsCreated { get; set; }
    public int ProjectsUpdated { get; set; }
    public int TasksCreated { get; set; }
    public int TasksUpdated { get; set; }
    public int TasksSoftDeleted { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class ListLinearSyncHistoryResponse
{
    public List<LinearSyncHistoryEntryResponse> Entries { get; set; } = new();
}

public sealed class ListUserIntegrationsResponse
{
    public List<UserIntegrationResponse> Integrations { get; set; } = new();
}
