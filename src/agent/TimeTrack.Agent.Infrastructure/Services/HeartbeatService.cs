using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Domain.Enums;
using TimeTrack.Agent.Domain.ValueObjects;

namespace TimeTrack.Agent.Infrastructure.Services;

/// <summary>
/// Envia heartbeat para o backend com informações do dispositivo.
/// Chamado a cada ciclo do SyncWorker (~60s).
/// </summary>
public sealed class HeartbeatService : IHeartbeatService
{
    private readonly HttpClient _httpClient;
    private readonly ITokenStore _tokenStore;
    private readonly ITrackingStateRepository _trackingStateRepository;
    private readonly ICurrentUserContext _userContext;
    private readonly ILogger<HeartbeatService> _logger;

    private static readonly JsonSerializerOptions JsonReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public HeartbeatService(
        HttpClient httpClient,
        ITokenStore tokenStore,
        ITrackingStateRepository trackingStateRepository,
        ICurrentUserContext userContext,
        ILogger<HeartbeatService> logger)
    {
        _httpClient = httpClient;
        _tokenStore = tokenStore;
        _trackingStateRepository = trackingStateRepository;
        _userContext = userContext;
        _logger = logger;
    }

    public async Task<HeartbeatResult> SendHeartbeatAsync(AgentHealthSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        if (!_userContext.IsAuthenticated || !_userContext.DeviceId.HasValue)
            return new HeartbeatResult { Success = false };

        try
        {
            // Ensure valid token
            if (_tokenStore.IsJwtExpiringSoon(2))
                await _tokenStore.RefreshAsync(cancellationToken);

            var jwt = await _tokenStore.GetJwtAsync(cancellationToken);
            if (string.IsNullOrEmpty(jwt))
                return new HeartbeatResult { Success = false };

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", jwt);

            var trackingState = await GetTrackingStateAsync(cancellationToken);

            var payload = new
            {
                agentVersion = GetAgentVersion(),
                osVersion = GetFriendlyOsVersion(),
                ipAddress = GetLocalIpAddress(),
                uptimeSeconds = GetUptimeSeconds(),
                trackingState,
                healthStatus = snapshot.HealthStatus,
                backendReachable = snapshot.BackendReachable,
                consecutiveSyncFailures = snapshot.ConsecutiveSyncFailures,
                lastSuccessfulSyncAt = snapshot.LastSuccessfulSyncAt,
                ipcConnected = snapshot.IpcConnected
            };

            var deviceId = _userContext.DeviceId.Value;
            var response = await _httpClient.PostAsJsonAsync(
                $"/api/v1/devices/{deviceId}/heartbeat",
                payload,
                JsonWriteOptions,
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                if (await _tokenStore.RefreshAsync(cancellationToken))
                {
                    jwt = await _tokenStore.GetJwtAsync(cancellationToken);
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", jwt);

                    response = await _httpClient.PostAsJsonAsync(
                        $"/api/v1/devices/{deviceId}/heartbeat",
                        payload,
                        cancellationToken);
                }
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Heartbeat failed with HTTP {Status} for device {DeviceId}", response.StatusCode, deviceId);
                return new HeartbeatResult { Success = false };
            }

            var result = await response.Content.ReadFromJsonAsync<HeartbeatResponse>(JsonReadOptions, cancellationToken);

            _logger.LogDebug("Heartbeat sent: device={DeviceId}, trackingState={TrackingState}, hasPendingCommands={HasPending}",
                deviceId, trackingState, result?.HasPendingCommands ?? false);

            return new HeartbeatResult
            {
                Success = true,
                HasPendingCommands = result?.HasPendingCommands ?? false,
                SubscriptionStatus = result?.SubscriptionStatus ?? "active",
                GracePeriodEnd = result?.GracePeriodEnd
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Heartbeat failed (network/timeout). Will retry next cycle.");
            return new HeartbeatResult { Success = false };
        }
    }

    private async Task<string> GetTrackingStateAsync(CancellationToken ct)
    {
        try
        {
            var userId = _userContext.UserId;
            if (!userId.HasValue)
            {
                _logger.LogWarning("GetTrackingStateAsync: no userId in context — reporting 'unknown'");
                return "unknown";
            }

            var state = await _trackingStateRepository.GetAsync(userId.Value, ct);
            if (state == null)
            {
                _logger.LogWarning("GetTrackingStateAsync: no tracking_state row found for user {UserId} — reporting 'unknown'", userId.Value);
                return "unknown";
            }

            return state.Status switch
            {
                TrackingStatus.Active => "running",
                TrackingStatus.PausedByUser => "paused",
                TrackingStatus.PausedByPolicy => "paused",
                TrackingStatus.Disabled => "stopped",
                _ => "idle"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GetTrackingStateAsync failed — reporting 'unknown'");
            return "unknown";
        }
    }

    private static string GetAgentVersion()
    {
        return (System.Reflection.Assembly.GetEntryAssembly() ?? typeof(HeartbeatService).Assembly)
            .GetName().Version?.ToString(3) ?? "0.0.0";
    }

    private static string GetFriendlyOsVersion()
    {
        try
        {
            // Registry gives "Windows 11 Home", "Windows 10 Pro", etc.
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key != null)
            {
                var productName = key.GetValue("ProductName") as string;
                var displayVersion = key.GetValue("DisplayVersion") as string; // e.g. "23H2"
                if (!string.IsNullOrEmpty(productName))
                    return string.IsNullOrEmpty(displayVersion) ? productName : $"{productName} {displayVersion}";
            }
        }
        catch { }

        return Environment.OSVersion.VersionString;
    }

    private static int GetUptimeSeconds()
    {
        try
        {
            return (int)(DateTime.Now - Process.GetCurrentProcess().StartTime).TotalSeconds;
        }
        catch
        {
            return 0;
        }
    }

    private static string? GetLocalIpAddress()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect("8.8.8.8", 80);
            return (socket.LocalEndPoint as IPEndPoint)?.Address.ToString();
        }
        catch
        {
            return null;
        }
    }

    private sealed class HeartbeatResponse
    {
        public DateTime LastSeenAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool HasPendingCommands { get; set; }
        public string SubscriptionStatus { get; set; } = "active";
        public DateTime? GracePeriodEnd { get; set; }
    }
}
