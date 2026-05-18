using System.Net.Http.Headers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using TimeTrack.Agent.Contracts.Providers;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.AgentService.Configuration;

namespace TimeTrack.AgentService.Workers;

/// <summary>
/// Worker dedicado ao upload de evidências para o backend.
/// Roda separado do SyncWorker principal para não interferir com activity sessions.
/// Pipeline: solicita presigned URL → descriptografa arquivo local → PUT direto → confirma.
/// </summary>
public sealed class EvidenceUploadWorker : BackgroundService
{
    private readonly IEvidenceUploadQueue _queue;
    private readonly ILocalEncryptionService _encryptionService;
    private readonly IActiveWindowProvider _activeWindowProvider;
    private readonly ITokenStore _tokenStore;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<EvidenceUploadWorker> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _backendUrl;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public EvidenceUploadWorker(
        IEvidenceUploadQueue queue,
        ILocalEncryptionService encryptionService,
        IActiveWindowProvider activeWindowProvider,
        ITokenStore tokenStore,
        ICurrentUserContext currentUserContext,
        IOptions<AgentSettings> settings,
        ILogger<EvidenceUploadWorker> logger)
    {
        _queue = queue;
        _encryptionService = encryptionService;
        _activeWindowProvider = activeWindowProvider;
        _tokenStore = tokenStore;
        _currentUserContext = currentUserContext;
        _logger = logger;
        _backendUrl = settings.Value.Sync?.BackendUrl ?? "http://localhost:5000";

        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EvidenceUploadWorker started");

        // Cleanup old entries on startup
        await _queue.CleanupOldEntriesAsync(1, stoppingToken);

        // Cleanup orphan temp files
        CleanupOrphanFiles();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var pending = await _queue.GetPendingAsync(5, stoppingToken);

                foreach (var item in pending)
                {
                    if (stoppingToken.IsCancellationRequested) break;
                    await ProcessItemAsync(item, stoppingToken);
                }

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in EvidenceUploadWorker loop");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        _logger.LogInformation("EvidenceUploadWorker stopped");
    }

    private async Task ProcessItemAsync(EvidenceQueueItem item, CancellationToken ct)
    {
        try
        {
            // Step 1: Request presigned upload URL from backend
            var deviceId = _currentUserContext.DeviceId ?? Guid.Empty;
            var requestPayload = new
            {
                fileName = $"{item.Id}.jpg",
                evidenceType = item.EvidenceType,
                contentType = "image/jpeg",
                deviceId,
                appName = item.AppName,
                capturedAt = item.CapturedAt.Kind == DateTimeKind.Local
                    ? item.CapturedAt.ToUniversalTime()
                    : DateTime.SpecifyKind(item.CapturedAt, DateTimeKind.Utc),
                windowTitleHash = item.WindowTitleHash,
                fileSizeBytes = item.FileSizeBytes
            };

            var requestJson = JsonSerializer.Serialize(requestPayload);
            using var requestContent = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");

            var jwt = await _tokenStore.GetJwtAsync(ct);
            if (string.IsNullOrEmpty(jwt))
            {
                _logger.LogDebug("Skipping evidence upload {Id}: no auth token yet, will retry next cycle", item.Id);
                return;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_backendUrl}/api/v1/evidence/upload-url")
            {
                Content = requestContent
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

            using var requestResponse = await _httpClient.SendAsync(request, ct);

            if (requestResponse.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("Received 401 on evidence upload-url, attempting token refresh");
                if (await _tokenStore.RefreshAsync(ct))
                {
                    jwt = await _tokenStore.GetJwtAsync(ct);
                    using var retryRequest = new HttpRequestMessage(HttpMethod.Post, $"{_backendUrl}/api/v1/evidence/upload-url")
                    {
                        Content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json")
                    };
                    retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
                    requestResponse.Dispose();
                    var retryResponse = await _httpClient.SendAsync(retryRequest, ct);
                    // Continue with retryResponse
                    await HandleUploadResponseAsync(retryResponse, item, jwt!, ct);
                    return;
                }

                await _queue.MarkAsFailedAsync(item.Id, "Authentication failed after refresh", ct);
                return;
            }

            await HandleUploadResponseAsync(requestResponse, item, jwt!, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process evidence upload: {Id}", item.Id);
            await _queue.MarkAsFailedAsync(item.Id, ex.Message, ct);
        }
    }

    private async Task HandleUploadResponseAsync(
        HttpResponseMessage requestResponse, EvidenceQueueItem item, string jwt, CancellationToken ct)
    {
        if (!requestResponse.IsSuccessStatusCode)
        {
            var errorBody = await requestResponse.Content.ReadAsStringAsync(ct);
            await _queue.MarkAsFailedAsync(item.Id, $"Upload URL request failed: {requestResponse.StatusCode} - {errorBody}", ct);
            return;
        }

        var responseJson = await requestResponse.Content.ReadAsStringAsync(ct);
        var uploadData = JsonSerializer.Deserialize<PresignedUploadData>(responseJson, _jsonOptions);

        if (uploadData == null || string.IsNullOrEmpty(uploadData.UploadUrl))
        {
            await _queue.MarkAsFailedAsync(item.Id, $"Invalid upload URL response: {responseJson}", ct);
            return;
        }

        // Step 2: Decrypt local file
        if (!File.Exists(item.LocalPath))
        {
            await _queue.MarkAsFailedAsync(item.Id, $"Local file not found: {item.LocalPath}", ct);
            return;
        }

        var encryptedData = await File.ReadAllBytesAsync(item.LocalPath, ct);
        var ivPath = $"{item.LocalPath}.iv";
        byte[]? iv = null;
        if (File.Exists(ivPath))
        {
            iv = await File.ReadAllBytesAsync(ivPath, ct);
        }

        byte[] jpegData;
        if (iv != null)
        {
            jpegData = _encryptionService.Decrypt(encryptedData, iv);
        }
        else
        {
            jpegData = encryptedData;
        }

        // Step 3: Upload via presigned URL (PUT)
        using var uploadContent = new ByteArrayContent(jpegData);
        uploadContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");

        using var uploadResponse = await _httpClient.PutAsync(uploadData.UploadUrl, uploadContent, ct);

        if (!uploadResponse.IsSuccessStatusCode)
        {
            await _queue.MarkAsFailedAsync(item.Id, $"Upload to storage failed: {uploadResponse.StatusCode}", ct);
            return;
        }

        // Step 4: Confirm upload with backend
        var confirmPayload = new { fileSizeBytes = (long)jpegData.Length };
        var confirmJson = JsonSerializer.Serialize(confirmPayload);
        using var confirmRequest = new HttpRequestMessage(HttpMethod.Post,
            $"{_backendUrl}/api/v1/evidence/{uploadData.EvidenceId}/confirm-upload")
        {
            Content = new StringContent(confirmJson, System.Text.Encoding.UTF8, "application/json")
        };
        confirmRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        using var confirmResponse = await _httpClient.SendAsync(confirmRequest, ct);

        if (!confirmResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning("Confirm upload returned {Status} for evidence {Id}, but file was uploaded",
                confirmResponse.StatusCode, uploadData.EvidenceId);
        }

        // Step 5: Mark as uploaded and cleanup local files
        await _queue.MarkAsUploadedAsync(item.Id, ct);

        CleanupLocalFile(item.LocalPath);
        CleanupLocalFile($"{item.LocalPath}.iv");

        _logger.LogInformation("Evidence uploaded successfully: {Id} ({Size}KB)",
            item.Id, jpegData.Length / 1024);
    }

    private static void CleanupLocalFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch { /* best effort */ }
    }

    private void CleanupOrphanFiles()
    {
        try
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TimeTrack", "evidence_queue");

            if (!Directory.Exists(appDataPath)) return;

            var cutoff = DateTime.UtcNow.AddHours(-24);
            foreach (var file in Directory.GetFiles(appDataPath, "*.enc"))
            {
                if (File.GetCreationTimeUtc(file) < cutoff)
                {
                    _logger.LogInformation("Cleaning up orphan temp file: {File}", file);
                    try { File.Delete(file); } catch { }
                    var ivFile = $"{file}.iv";
                    if (File.Exists(ivFile))
                        try { File.Delete(ivFile); } catch { }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup orphan files");
        }
    }

    private sealed class PresignedUploadResponse
    {
        public bool Success { get; set; }
        public PresignedUploadData? Data { get; set; }
    }

    private sealed class PresignedUploadData
    {
        public string UploadUrl { get; set; } = string.Empty;
        public Guid EvidenceId { get; set; }
        public string StorageKey { get; set; } = string.Empty;
    }
}
