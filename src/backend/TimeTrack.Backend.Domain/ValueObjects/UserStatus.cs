namespace TimeTrack.Backend.Domain.ValueObjects;

/// <summary>
/// Status do usuário
/// </summary>
public enum UserStatus
{
    Active = 1,
    Inactive = 2,
    PendingPasswordChange = 3
}
