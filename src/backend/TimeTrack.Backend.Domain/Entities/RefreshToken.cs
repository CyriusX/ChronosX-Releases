namespace TimeTrack.Backend.Domain.Entities;

/// <summary>
/// Representa um refresh token para autenticação
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid DeviceId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Navigation properties
    public User? User { get; private set; }

    private RefreshToken() { }

    public static RefreshToken Create(
        Guid userId,
        Guid deviceId,
        string tokenHash,
        TimeSpan expiresIn)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("Token hash is required", nameof(tokenHash));

        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DeviceId = deviceId,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.Add(expiresIn),
            CreatedAt = DateTime.UtcNow
        };
    }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    public bool IsRevoked => RevokedAt.HasValue;

    public bool IsValid => !IsExpired && !IsRevoked;

    public void Revoke()
    {
        if (!IsRevoked)
        {
            RevokedAt = DateTime.UtcNow;
        }
    }
}
