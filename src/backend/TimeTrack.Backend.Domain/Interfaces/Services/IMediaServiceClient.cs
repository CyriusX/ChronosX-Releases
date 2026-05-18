namespace TimeTrack.Backend.Domain.Interfaces.Services;

/// <summary>
/// Client for communicating with the Media microservice (AppRelationshipMedia).
/// Handles presigned URL generation, upload confirmation and media deletion via HTTP.
/// </summary>
public interface IMediaServiceClient
{
    Task<PresignedUploadResult> RequestUploadUrlAsync(
        string fileName, string mimeType, long fileSizeBytes,
        string mediaType, CancellationToken ct);

    Task ConfirmUploadAsync(
        string mediaId, string key, CancellationToken ct);

    Task<MediaInfo?> GetMediaAsync(
        string mediaId, CancellationToken ct);

    Task<PresignedDownloadResult?> GetPresignedDownloadUrlAsync(
        string mediaId, CancellationToken ct);

    Task DeleteMediaAsync(
        string mediaId, CancellationToken ct);
}

public sealed record PresignedUploadResult(
    string MediaId,
    string UploadUrl,
    string Key,
    DateTime ExpiresAt,
    int ExpiresInSeconds
);

public sealed record MediaInfo(
    string Id,
    string Key,
    string MimeType,
    long SizeBytes,
    string Type,
    string Status,
    string? Url,
    string? ThumbnailUrl,
    DateTime CreatedAt
);

public sealed record PresignedDownloadResult(
    string DownloadUrl,
    int ExpiresInSeconds
);
