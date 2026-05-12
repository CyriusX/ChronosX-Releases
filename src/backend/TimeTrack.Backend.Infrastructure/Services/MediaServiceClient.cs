using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Interfaces.Services;

namespace TimeTrack.Backend.Infrastructure.Services;

public sealed class MediaServiceClient : IMediaServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly IMediaServiceConfiguration _config;
    private readonly ILogger<MediaServiceClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public MediaServiceClient(
        HttpClient httpClient,
        IMediaServiceConfiguration config,
        ILogger<MediaServiceClient> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_config.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_config.RequestTimeoutSeconds);

        if (!string.IsNullOrWhiteSpace(_config.ApiKey))
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _config.ApiKey);
    }

    public async Task<PresignedUploadResult> RequestUploadUrlAsync(
        string fileName, string mimeType, long fileSizeBytes,
        string mediaType, CancellationToken ct)
    {
        var payload = new
        {
            fileName,
            mimeType,
            fileSize = fileSizeBytes,
            type = mediaType
        };

        var response = await _httpClient.PostAsJsonAsync(
            "/api/v1/media/request-upload", payload, JsonOptions, ct);

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<MediaServiceResponse<RequestUploadData>>(JsonOptions, ct);

        if (body?.Data == null)
            throw new InvalidOperationException("Media service returned empty response for request-upload");

        _logger.LogInformation(
            "Requested upload URL from media service. MediaId={MediaId}, Key={Key}",
            body.Data.MediaId, body.Data.Key);

        return new PresignedUploadResult(
            body.Data.MediaId,
            body.Data.UploadUrl,
            body.Data.Key,
            body.Data.ExpiresAt,
            body.Data.ExpiresInSeconds
        );
    }

    public async Task ConfirmUploadAsync(string mediaId, string key, CancellationToken ct)
    {
        var payload = new { key };

        var response = await _httpClient.PostAsJsonAsync(
            $"/api/v1/media/{mediaId}/confirm", payload, JsonOptions, ct);

        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Confirmed upload for media {MediaId}", mediaId);
    }

    public async Task<MediaInfo?> GetMediaAsync(string mediaId, CancellationToken ct)
    {
        var response = await _httpClient.GetAsync($"/api/v1/media/{mediaId}", ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<MediaServiceResponse<MediaData>>(JsonOptions, ct);

        if (body?.Data == null)
            return null;

        return new MediaInfo(
            body.Data.Id,
            body.Data.Key,
            body.Data.MimeType,
            body.Data.SizeBytes,
            body.Data.Type,
            body.Data.Status,
            body.Data.Url,
            body.Data.ThumbnailUrl,
            body.Data.CreatedAt
        );
    }

    public async Task DeleteMediaAsync(string mediaId, CancellationToken ct)
    {
        var response = await _httpClient.DeleteAsync($"/api/v1/media/{mediaId}", ct);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Deleted media {MediaId} via media service", mediaId);
    }

    public async Task<PresignedDownloadResult?> GetPresignedDownloadUrlAsync(string mediaId, CancellationToken ct)
    {
        var response = await _httpClient.GetAsync($"/api/v1/media/{mediaId}/download-url", ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<MediaServiceResponse<PresignedDownloadData>>(JsonOptions, ct);

        if (body?.Data == null)
            return null;

        return new PresignedDownloadResult(body.Data.DownloadUrl, body.Data.ExpiresInSeconds);
    }

    // Response DTOs matching the Media Service contract
    private sealed record MediaServiceResponse<T>(bool Success, T? Data);

    private sealed class RequestUploadData
    {
        public string MediaId { get; set; } = string.Empty;
        public string UploadUrl { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public int ExpiresInSeconds { get; set; }
    }

    private sealed class MediaData
    {
        public string Id { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string MimeType { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Url { get; set; }
        public string? ThumbnailUrl { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    private sealed class PresignedDownloadData
    {
        public string MediaId { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public int ExpiresInSeconds { get; set; }
    }
}
