namespace TimeTrack.Backend.Application.Evidence.DTOs;

public sealed record PresignedUploadUrlResponse(
    string UploadUrl,
    Guid EvidenceId,
    string StorageKey
);

public sealed record PresignedDownloadUrlResponse(
    string DownloadUrl,
    EvidenceItemResponse EvidenceItem
);

public sealed record RequestPresignedUploadUrlRequest(
    string FileName,
    string EvidenceType,
    string ContentType,
    Guid DeviceId,
    string AppName,
    DateTime CapturedAt,
    string? WindowTitleHash,
    long FileSizeBytes = 1
);

public sealed record ConfirmUploadRequest(
    long FileSizeBytes
);

public sealed record EvidenceItemResponse
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string EvidenceType { get; init; } = string.Empty;
    public DateTime CapturedAt { get; init; }
    public string AppName { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public string? ThumbnailUrl { get; init; }
    public string? DownloadUrl { get; init; }
    public DateTime CreatedAt { get; init; }
}
