namespace TimeTrack.Backend.Domain.Entities;

/// <summary>
/// Platform-level API keys (SysAdmin-only) used for read-only ops integrations (e.g., MCP).
/// </summary>
public sealed class PlatformApiKey
{
    public Guid Id { get; private set; }
    public string Label { get; private set; } = string.Empty;
    public string KeyHash { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public DateTime? LastUsedAtUtc { get; private set; }

    private PlatformApiKey() { }

    public static PlatformApiKey Create(Guid id, string label, string keyHash)
    {
        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Label is required", nameof(label));

        if (string.IsNullOrWhiteSpace(keyHash))
            throw new ArgumentException("Key hash is required", nameof(keyHash));

        return new PlatformApiKey
        {
            Id = id,
            Label = label.Trim(),
            KeyHash = keyHash.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void Revoke()
    {
        if (RevokedAtUtc.HasValue) return;
        RevokedAtUtc = DateTime.UtcNow;
    }

    public void RecordUsed()
    {
        LastUsedAtUtc = DateTime.UtcNow;
    }
}

