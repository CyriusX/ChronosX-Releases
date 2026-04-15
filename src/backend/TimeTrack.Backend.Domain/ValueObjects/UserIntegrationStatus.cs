namespace TimeTrack.Backend.Domain.ValueObjects;

/// <summary>
/// Lifecycle state of a UserIntegration row.
/// </summary>
public enum UserIntegrationStatus
{
    Active = 1,
    Revoked = 2,
    ErrorUnauthorized = 3
}
