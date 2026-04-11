using TimeTrack.Backend.Application.Integrations.DTOs;
using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Application.Integrations;

internal static class IntegrationMapper
{
    public static UserIntegrationResponse Map(Domain.Entities.UserIntegration i) => new()
    {
        Id = i.Id,
        Provider = i.Provider.ToString(),
        Status = i.Status.ToString(),
        ErrorMessage = i.ErrorMessage,
        ExternalUserId = i.ExternalUserId,
        ExternalUserName = i.ExternalUserName,
        ExternalUserEmail = i.ExternalUserEmail,
        ConnectedAt = i.ConnectedAt,
        LastSyncAt = i.LastSyncAt,
        LastUsedAt = i.LastUsedAt,
        AuthMethod = i.AuthMethod switch
        {
            Domain.Entities.UserIntegrationAuthMethod.ApiKey => "ApiKey",
            Domain.Entities.UserIntegrationAuthMethod.OAuth => "OAuth",
            _ => "ApiKey"
        }
    };

    public static LinearSyncHistoryEntryResponse Map(LinearSyncHistory h) => new()
    {
        Id = h.Id,
        StartedAt = h.StartedAt,
        FinishedAt = h.FinishedAt,
        DurationMs = h.DurationMs,
        ProjectsCreated = h.ProjectsCreated,
        ProjectsUpdated = h.ProjectsUpdated,
        TasksCreated = h.TasksCreated,
        TasksUpdated = h.TasksUpdated,
        TasksSoftDeleted = h.TasksSoftDeleted,
        Success = h.Success,
        ErrorMessage = h.ErrorMessage
    };
}
