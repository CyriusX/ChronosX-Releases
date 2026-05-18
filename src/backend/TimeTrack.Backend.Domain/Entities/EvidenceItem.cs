namespace TimeTrack.Backend.Domain.Entities;

public sealed class EvidenceItem
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid OrgId { get; private set; }
    public Guid DeviceId { get; private set; }
    public string EvidenceType { get; private set; } = "screenshot";
    public string StorageKey { get; private set; } = string.Empty;
    public string? ExternalMediaId { get; private set; }
    public DateTime CapturedAt { get; private set; }
    public string AppName { get; private set; } = string.Empty;
    public string? WindowTitleHash { get; private set; }
    public long FileSizeBytes { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Navigation
    public User? User { get; private set; }
    public Device? Device { get; private set; }

    private EvidenceItem() { }

    public static EvidenceItem Create(
        Guid id,
        Guid userId,
        Guid orgId,
        Guid deviceId,
        string evidenceType,
        string storageKey,
        DateTime capturedAt,
        string appName,
        string? windowTitleHash,
        long fileSizeBytes = 0)
    {
        if (string.IsNullOrWhiteSpace(evidenceType))
            throw new ArgumentException("Evidence type is required", nameof(evidenceType));

        if (string.IsNullOrWhiteSpace(storageKey))
            throw new ArgumentException("Storage key is required", nameof(storageKey));

        return new EvidenceItem
        {
            Id = id,
            UserId = userId,
            OrgId = orgId,
            DeviceId = deviceId,
            EvidenceType = evidenceType,
            StorageKey = storageKey,
            CapturedAt = capturedAt,
            AppName = appName,
            WindowTitleHash = windowTitleHash,
            FileSizeBytes = fileSizeBytes,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void SetFileSize(long sizeBytes)
    {
        FileSizeBytes = sizeBytes;
    }

    public void SetExternalMediaId(string mediaId)
    {
        ExternalMediaId = mediaId;
    }

    public void SoftDelete()
    {
        if (IsDeleted) return;
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
    }
}
