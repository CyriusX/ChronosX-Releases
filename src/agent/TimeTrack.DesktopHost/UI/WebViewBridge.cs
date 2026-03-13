using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.DesktopHost.Ipc;

namespace TimeTrack.DesktopHost.UI;

/// <summary>
/// Bridge object exposed to JavaScript via WebView2
/// Provides IPC communication between React UI and AgentService
///
/// IMPORTANT: Methods must return Task&lt;string&gt; for WebView2 to convert them to Promises in JavaScript.
/// All methods return JSON strings that can be parsed by the JavaScript side.
/// </summary>
[ClassInterface(ClassInterfaceType.AutoDual)]
[ComVisible(true)]
public sealed class WebViewBridge
{
    private readonly IIpcClient _ipcClient;
    private readonly ILogger<WebViewBridge> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public WebViewBridge(IIpcClient ipcClient, ILogger<WebViewBridge> logger)
    {
        _ipcClient = ipcClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        // Subscribe to events from AgentService
        _ipcClient.EventReceived += OnEventReceived;
        _ipcClient.ConnectionStateChanged += OnConnectionStateChanged;
    }

    /// <summary>
    /// Gets the current connection status to AgentService
    /// </summary>
    public bool IsConnected => _ipcClient.IsConnected;

    /// <summary>
    /// Sends a command to AgentService and returns JSON response
    /// </summary>
    public async Task<string> SendCommand(string command, string? payloadJson = null)
    {
        try
        {
            _logger.LogInformation("SendCommand called: {Command}, Payload: {Payload}", command, payloadJson ?? "null");

            object? payload = null;
            if (!string.IsNullOrEmpty(payloadJson))
            {
                payload = JsonSerializer.Deserialize<object>(payloadJson);
            }

            _logger.LogInformation("Calling _ipcClient.SendCommandAsync, IsConnected: {IsConnected}", _ipcClient.IsConnected);
            var response = await _ipcClient.SendCommandAsync(command, payload);
            _logger.LogInformation("SendCommandAsync returned: {Success}", response.Success);

            var result = new
            {
                success = response.Success,
                data = response.Data,
                error = response.Error
            };

            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending command: {Command}", command);

            var result = new
            {
                success = false,
                data = (object?)null,
                error = ex.Message
            };

            return JsonSerializer.Serialize(result, _jsonOptions);
        }
    }

    /// <summary>
    /// Sends a query to AgentService and returns JSON response
    /// </summary>
    public async Task<string> SendQuery(string query, string? payloadJson = null)
    {
        try
        {
            _logger.LogDebug("SendQuery called: {Query}", query);

            object? payload = null;
            if (!string.IsNullOrEmpty(payloadJson))
            {
                payload = JsonSerializer.Deserialize<object>(payloadJson);
            }

            var response = await _ipcClient.SendQueryAsync(query, payload);

            var result = new
            {
                success = response.Success,
                data = response.Data,
                error = response.Error
            };

            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending query: {Query}", query);

            var result = new
            {
                success = false,
                data = (object?)null,
                error = ex.Message
            };

            return JsonSerializer.Serialize(result, _jsonOptions);
        }
    }

    /// <summary>
    /// Reconnects to AgentService
    /// </summary>
    public async Task<string> Reconnect()
    {
        try
        {
            _logger.LogDebug("Reconnect called");
            await _ipcClient.ConnectAsync();

            var result = new
            {
                success = true,
                data = (object?)null,
                error = (string?)null
            };

            return JsonSerializer.Serialize(result, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reconnecting");

            var result = new
            {
                success = false,
                data = (object?)null,
                error = ex.Message
            };

            return JsonSerializer.Serialize(result, _jsonOptions);
        }
    }

    /// <summary>
    /// Opens an external URL in the default browser
    /// </summary>
    public void OpenExternal(string url)
    {
        try
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening external URL: {Url}", url);
        }
    }

    private void OnEventReceived(object? sender, IpcEventArgs e)
    {
        try
        {
            var eventJson = JsonSerializer.Serialize(new
            {
                eventType = e.EventType,
                payload = e.Payload
            }, _jsonOptions);

            _ = InvokeEventAsync(eventJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing event: {EventType}", e.EventType);
        }
    }

    private void OnConnectionStateChanged(object? sender, bool isConnected)
    {
        try
        {
            var eventJson = JsonSerializer.Serialize(new
            {
                eventType = "connectionStateChanged",
                payload = new { isConnected }
            }, _jsonOptions);

            _ = InvokeEventAsync(eventJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing connection state change");
        }
    }

    private async Task InvokeEventAsync(string eventJson)
    {
        // This will be called from WebView2 via JavaScript interop
        // The actual invocation happens in MainForm via CoreWebView2.ExecuteScriptAsync
        await Task.CompletedTask;
    }
}
