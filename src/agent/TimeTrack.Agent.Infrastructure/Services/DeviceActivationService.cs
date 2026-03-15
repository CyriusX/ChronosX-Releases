using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Configuration;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Domain.ValueObjects;

namespace TimeTrack.Agent.Infrastructure.Services;

/// <summary>
/// Implementação do serviço de ativação do dispositivo
/// </summary>
public sealed class DeviceActivationService : IDeviceActivationService
{
    private readonly HttpClient _httpClient;
    private readonly SyncSettings _settings;
    private readonly ILogger<DeviceActivationService> _logger;
    private readonly ITokenStore _tokenStore;
    private readonly JsonSerializerOptions _jsonOptions;

    private bool _isDeviceActivated;

    public bool IsDeviceActivated => _isDeviceActivated;

    public DeviceActivationService(
        HttpClient httpClient,
        SyncSettings settings,
        ILogger<DeviceActivationService> logger,
        ITokenStore tokenStore)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        _httpClient.BaseAddress = new Uri(_settings.BackendUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.HttpTimeoutSeconds);
    }

    public async Task<DeviceActivationResult> ActivateDeviceAsync(
        string jwt,
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var deviceId = DeviceId.Current.Value;
            if (!Guid.TryParse(deviceId, out var deviceIdGuid))
            {
                _logger.LogError("Invalid device ID format: {DeviceId}", deviceId);
                return DeviceActivationResult.Failure("Invalid device ID format");
            }

            var request = new ActivateDeviceRequest
            {
                DeviceId = deviceIdGuid,
                Hostname = Environment.MachineName,
                DeviceName = Environment.MachineName,
                AgentVersion = GetAgentVersion(),
                DisplayMode = "background"
            };

            _logger.LogInformation(
                "Activating device {DeviceId} on backend {BackendUrl}",
                deviceId,
                _settings.BackendUrl);

            // Set authorization header with current JWT
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", jwt);

            var content = new StringContent(
                JsonSerializer.Serialize(request, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(
                "/api/v1/devices/activate",
                content,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "Device activation failed with status {StatusCode}: {Error}",
                    response.StatusCode,
                    errorContent);

                return DeviceActivationResult.Failure(
                    $"Activation failed with status {(int)response.StatusCode}: {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var activationResponse = JsonSerializer.Deserialize<ActivateDeviceResponse>(
                responseContent,
                _jsonOptions);

            if (activationResponse == null)
            {
                _logger.LogError("Failed to deserialize activation response");
                return DeviceActivationResult.Failure("Invalid activation response");
            }

            _logger.LogInformation(
                "Device activated successfully. DeviceId: {DeviceId}, Status: {Status}",
                activationResponse.DeviceId,
                activationResponse.Status);

            // Store new tokens (they include device_id in JWT)
            await _tokenStore.StoreTokensAsync(
                activationResponse.AccessToken,
                activationResponse.RefreshToken,
                cancellationToken);

            _isDeviceActivated = true;

            return DeviceActivationResult.Success(
                activationResponse.AccessToken,
                activationResponse.RefreshToken,
                activationResponse.DeviceId,
                activationResponse.Status);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during device activation");
            return DeviceActivationResult.Failure($"HTTP error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during device activation");
            return DeviceActivationResult.Failure($"Unexpected error: {ex.Message}");
        }
    }

    private static string GetAgentVersion()
    {
        // Get version from entry assembly or default to 1.0.0
        var assembly = System.Reflection.Assembly.GetEntryAssembly();
        var version = assembly?.GetName().Version;
        return version?.ToString() ?? "1.0.0";
    }

    #region DTOs

    private sealed class ActivateDeviceRequest
    {
        public Guid DeviceId { get; set; }
        public string Hostname { get; set; } = string.Empty;
        public string? DeviceName { get; set; }
        public string AgentVersion { get; set; } = string.Empty;
        public string DisplayMode { get; set; } = "background";
    }

    private sealed class ActivateDeviceResponse
    {
        public Guid DeviceId { get; set; }
        public DateTime ActivatedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
    }

    #endregion
}
