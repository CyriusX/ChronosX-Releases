namespace TimeTrack.Backend.Domain.Entities;

/// <summary>
/// Representa um token de reset de senha
/// </summary>
public sealed class PasswordResetToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? UsedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Navigation property
    public User? User { get; private set; }

    private PasswordResetToken() { }

    public static PasswordResetToken Create(
        Guid userId,
        string tokenHash,
        TimeSpan expiresIn)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("Token hash is required", nameof(tokenHash));

        return new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.Add(expiresIn),
            CreatedAt = DateTime.UtcNow
        };
    }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    public bool IsUsed => UsedAt.HasValue;

    public bool IsValid => !IsExpired && !IsUsed;

    public void MarkAsUsed()
    {
        if (!IsUsed)
        {
            UsedAt = DateTime.UtcNow;
        }
    }
}
