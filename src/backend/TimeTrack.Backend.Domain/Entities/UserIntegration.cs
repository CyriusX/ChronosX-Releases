using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Domain.Entities;

/// <summary>
/// A single user's connection to a third-party service (Linear for v1).
/// The encrypted API token lets the backend act on behalf of the user
/// when syncing and pushing state changes.
/// </summary>
public sealed class UserIntegration
{
    public Guid Id { get; private set; }
    public Guid OrgId { get; private set; }
    public Guid UserId { get; private set; }
    public UserIntegrationProvider Provider { get; private set; }

    /// <summary>Stable external identifier (Linear: viewer.id).</summary>
    public string ExternalUserId { get; private set; } = string.Empty;
    public string? ExternalUserName { get; private set; }
    public string? ExternalUserEmail { get; private set; }

    /// <summary>
    /// The user's personal API key, encrypted at rest via
    /// <c>IDataProtector</c> (purpose: "Linear.ApiKey.v1").
    /// </summary>
    public byte[] EncryptedToken { get; private set; } = Array.Empty<byte>();

    /// <summary>Reserved for future OAuth scope strings.</summary>
    public string? Scope { get; private set; }

    public DateTime ConnectedAt { get; private set; }
    public DateTime? LastUsedAt { get; private set; }
    public DateTime? LastSyncAt { get; private set; }

    public UserIntegrationStatus Status { get; private set; }
    public string? ErrorMessage { get; private set; }

    /// <summary>JSON blob for provider-specific metadata (e.g. cached Linear team state list).</summary>
    public string? MetadataJson { get; private set; }

    public User? User { get; private set; }

    private UserIntegration() { }

    public static UserIntegration Create(
        Guid orgId,
        Guid userId,
        UserIntegrationProvider provider,
        string externalUserId,
        string? externalUserName,
        string? externalUserEmail,
        byte[] encryptedToken,
        string? scope = null,
        string? metadataJson = null)
    {
        if (string.IsNullOrWhiteSpace(externalUserId))
            throw new ArgumentException("External user id is required", nameof(externalUserId));
        if (encryptedToken is null || encryptedToken.Length == 0)
            throw new ArgumentException("Encrypted token is required", nameof(encryptedToken));

        return new UserIntegration
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            UserId = userId,
            Provider = provider,
            ExternalUserId = externalUserId.Trim(),
            ExternalUserName = externalUserName?.Trim(),
            ExternalUserEmail = externalUserEmail?.Trim(),
            EncryptedToken = encryptedToken,
            Scope = scope,
            MetadataJson = metadataJson,
            ConnectedAt = DateTime.UtcNow,
            Status = UserIntegrationStatus.Active
        };
    }

    public void ReplaceToken(byte[] newEncryptedToken, string externalUserId, string? externalUserName, string? externalUserEmail)
    {
        if (newEncryptedToken is null || newEncryptedToken.Length == 0)
            throw new ArgumentException("Encrypted token is required", nameof(newEncryptedToken));

        EncryptedToken = newEncryptedToken;
        ExternalUserId = externalUserId.Trim();
        ExternalUserName = externalUserName?.Trim();
        ExternalUserEmail = externalUserEmail?.Trim();
        Status = UserIntegrationStatus.Active;
        ErrorMessage = null;
        ConnectedAt = DateTime.UtcNow;
    }

    public void MarkUsed() => LastUsedAt = DateTime.UtcNow;

    public void MarkSynced()
    {
        LastSyncAt = DateTime.UtcNow;
        Status = UserIntegrationStatus.Active;
        ErrorMessage = null;
    }

    public void MarkRevoked()
    {
        Status = UserIntegrationStatus.Revoked;
        ErrorMessage = null;
    }

    public void MarkUnauthorized(string? message)
    {
        Status = UserIntegrationStatus.ErrorUnauthorized;
        ErrorMessage = message;
    }

    public void UpdateMetadata(string? metadataJson)
    {
        MetadataJson = metadataJson;
    }
}
