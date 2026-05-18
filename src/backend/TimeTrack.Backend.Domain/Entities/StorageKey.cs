namespace TimeTrack.Backend.Domain.Entities;

public sealed class StorageKey
{
    public Guid Id { get; private set; }
    public Guid OrgId { get; private set; }
    public string Bucket { get; private set; } = string.Empty;
    public string Key { get; private set; } = string.Empty;
    public string Region { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    // Navigation
    public Organization? Organization { get; private set; }

    private StorageKey() { }

    public static StorageKey Create(Guid orgId, string bucket, string key, string region)
    {
        if (string.IsNullOrWhiteSpace(bucket))
            throw new ArgumentException("Bucket is required", nameof(bucket));

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key is required", nameof(key));

        return new StorageKey
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Bucket = bucket,
            Key = key,
            Region = region,
            CreatedAt = DateTime.UtcNow
        };
    }
}
