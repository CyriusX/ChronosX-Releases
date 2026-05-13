using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Domain.Entities;

public sealed class OrgInviteLink
{
    public Guid Id { get; private set; }
    public Guid OrgId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public int? MaxUses { get; private set; }
    public int UseCount { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Navigation properties
    public Organization? Organization { get; private set; }

    private OrgInviteLink() { }

    public static OrgInviteLink Create(
        Guid orgId,
        string tokenHash,
        UserRole role,
        Guid createdByUserId,
        DateTime? expiresAt = null,
        int? maxUses = null)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("Token hash is required", nameof(tokenHash));

        return new OrgInviteLink
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            TokenHash = tokenHash,
            Role = role,
            CreatedByUserId = createdByUserId,
            ExpiresAt = expiresAt,
            MaxUses = maxUses,
            UseCount = 0,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow >= ExpiresAt.Value;

    public bool IsValid => IsActive && !IsExpired && (MaxUses == null || UseCount < MaxUses);

    public void Revoke()
    {
        IsActive = false;
    }

    public void IncrementUseCount()
    {
        UseCount++;
    }
}
