namespace TimeTrack.Backend.Domain.Entities;

/// <summary>
/// Representa uma chave de idempotência para deduplicação de requests
/// </summary>
public sealed class IdempotencyKey
{
    public Guid Id { get; private set; }
    public Guid OrgId { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string EntityType { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    private IdempotencyKey() { }

    public static IdempotencyKey Create(
        Guid orgId,
        string key,
        string entityType,
        Guid entityId)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key is required", nameof(key));

        return new IdempotencyKey
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Key = key,
            EntityType = entityType,
            EntityId = entityId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7) // Expires after 7 days
        };
    }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
}
