using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TimeTrack.MacOSDesktopHost.Configuration;

namespace TimeTrack.MacOSDesktopHost.Ipc;

public sealed class IpcResponse
{
    public bool Success { get; init; }
    public JsonElement? Data { get; init; }
    public string? Error { get; init; }
}

public sealed class IpcEventArgs : EventArgs
{
    public string EventType { get; init; } = string.Empty;
    public JsonElement Payload { get; init; }
}

public interface IIpcClient
{
    bool IsConnected { get; }
    event EventHandler<IpcEventArgs>? EventReceived;
    event EventHandler<bool>? ConnectionStateChanged;
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync();
    Task<IpcResponse> SendCommandAsync(string command, object? payload = null, CancellationToken cancellationToken = default);
    Task<IpcResponse> SendQueryAsync(string query, object? payload = null, CancellationToken cancellationToken = default);
}

public interface IpcClientHostedService : IHostedService
{
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync();
}

public sealed class UnixDomainSocketIpcClient : IIpcClient, IpcClientHostedService, IDisposable
{
    private readonly MacDesktopHostSettings _settings;
    private readonly ILogger<UnixDomainSocketIpcClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    private Socket? _socket;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private CancellationTokenSource? _listenCts;
    private Task? _listenTask;
    private CancellationToken _hostCancellationToken;
    private int _requestId;
    private readonly Dictionary<int, TaskCompletionSource<IpcResponse>> _pendingRequests = new();
    private readonly object _lock = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private bool _isConnected;
    public bool IsConnected => _isConnected;

    public event EventHandler<IpcEventArgs>? EventReceived;
    public event EventHandler<bool>? ConnectionStateChanged;

    public UnixDomainSocketIpcClient(MacDesktopHostSettings settings, ILogger<UnixDomainSocketIpcClient> logger)
    {
        _settings = settings;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        var attempts = 0;
        var maxAttempts = _settings.MaxReconnectionAttempts;

        while (attempts < maxAttempts)
        {
            try
            {
                _logger.LogDebug("Attempting to connect to socket '{SocketPath}' (attempt {Attempt}/{Max})",
                    _settings.SocketPath, attempts + 1, maxAttempts);

                _socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
                var endpoint = new UnixDomainSocketEndPoint(_settings.SocketPath);

                await _socket.ConnectAsync(endpoint, cancellationToken);

                var stream = new NetworkStream(_socket);
                _reader = new StreamReader(stream, Encoding.UTF8);
                _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                SetConnected(true);
                StartListening();

                _logger.LogInformation("Connected to AgentService via Unix Domain Socket '{SocketPath}'",
                    _settings.SocketPath);
                return;
            }
            catch (Exception ex)
            {
                attempts++;
                _logger.LogWarning(ex, "Failed to connect to AgentService (attempt {Attempt}/{Max})",
                    attempts, maxAttempts);

                try { _socket?.Dispose(); _socket = null; } catch { }

                if (attempts < maxAttempts)
                {
                    await Task.Delay(_settings.ReconnectionDelayMs, cancellationToken);
                }
            }
        }

        throw new InvalidOperationException(
            $"Failed to connect to AgentService after {maxAttempts} attempts");
    }

    public async Task DisconnectAsync()
    {
        StopListening();
        SetConnected(false);

        try
        {
            _writer?.Dispose();
            _reader?.Dispose();
            _socket?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during disconnect");
        }

        _writer = null;
        _reader = null;
        _socket = null;

        _logger.LogInformation("Disconnected from AgentService");
    }

    public async Task<IpcResponse> SendCommandAsync(string command, object? payload = null,
        CancellationToken cancellationToken = default)
    {
        return await SendMessageAsync("command", command, payload, cancellationToken);
    }

    public async Task<IpcResponse> SendQueryAsync(string query, object? payload = null,
        CancellationToken cancellationToken = default)
    {
        return await SendMessageAsync("query", query, payload, cancellationToken);
    }

    private async Task<IpcResponse> SendMessageAsync(string type, string name, object? payload,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("SendMessageAsync: Type={Type}, Name={Name}, IsConnected={IsConnected}",
            type, name, IsConnected);

        if (!IsConnected || _writer == null)
        {
            _logger.LogWarning("SendMessageAsync: Not connected");
            return new IpcResponse { Success = false, Error = "Not connected to AgentService" };
        }

        var requestId = Interlocked.Increment(ref _requestId);
        var tcs = new TaskCompletionSource<IpcResponse>();

        lock (_lock)
        {
            _pendingRequests[requestId] = tcs;
        }

        try
        {
            var message = new
            {
                RequestId = requestId,
                Type = type,
                Name = name,
                Payload = payload
            };

            var json = JsonSerializer.Serialize(message, _jsonOptions);
            _logger.LogInformation("Sending IPC message: {Message}", json);

            await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await _writer!.WriteLineAsync(json).ConfigureAwait(false);
                await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Message sent and flushed");
            }
            finally
            {
                _writeLock.Release();
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(30));

            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, cts.Token));

            if (completedTask != tcs.Task)
            {
                return new IpcResponse { Success = false, Error = "Request timeout" };
            }

            return await tcs.Task;
        }
        finally
        {
            lock (_lock)
            {
                _pendingRequests.Remove(requestId);
            }
        }
    }

    private void StartListening()
    {
        _listenCts = new CancellationTokenSource();
        _listenTask = ListenAsync(_listenCts.Token);
    }

    private void StopListening()
    {
        _listenCts?.Cancel();
        _listenTask?.Wait(TimeSpan.FromSeconds(5));
        _listenCts?.Dispose();
        _listenCts = null;
        _listenTask = null;
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ListenAsync started");
        try
        {
            while (!cancellationToken.IsCancellationRequested && _reader != null)
            {
                _logger.LogDebug("ListenAsync: Waiting for message...");
                var line = await _reader.ReadLineAsync(cancellationToken);
                if (line == null)
                {
                    _logger.LogWarning("Socket closed by server");
                    SetConnected(false);
                    break;
                }

                _logger.LogInformation("ListenAsync: Received message: {Message}",
                    line.Length > 200 ? line[..200] + "..." : line);
                ProcessIncomingMessage(line);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in listen loop");
            SetConnected(false);
        }

        if (!cancellationToken.IsCancellationRequested && !_hostCancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("ListenAsync: socket closed unexpectedly, scheduling reconnect");
            _ = ReconnectAsync();
        }
    }

    private async Task ReconnectAsync()
    {
        while (!_hostCancellationToken.IsCancellationRequested && !_isConnected)
        {
            try
            {
                _logger.LogInformation("ReconnectAsync: waiting {Delay}ms before reconnect attempt",
                    _settings.ReconnectionDelayMs);
                await Task.Delay(_settings.ReconnectionDelayMs, _hostCancellationToken);

                _writer?.Dispose(); _writer = null;
                _reader?.Dispose(); _reader = null;
                _socket?.Dispose(); _socket = null;

                _socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
                var endpoint = new UnixDomainSocketEndPoint(_settings.SocketPath);
                await _socket.ConnectAsync(endpoint, _hostCancellationToken);

                var stream = new NetworkStream(_socket);
                _reader = new StreamReader(stream, Encoding.UTF8);
                _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                SetConnected(true);
                StartListening();

                _logger.LogInformation("ReconnectAsync: reconnected to AgentService");
                return;
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ReconnectAsync: attempt failed, will retry");
                try { _socket?.Dispose(); _socket = null; } catch { }
            }
        }
    }

    private void ProcessIncomingMessage(string json)
    {
        try
        {
            var message = JsonDocument.Parse(json);
            var root = message.RootElement;

            if (root.TryGetProperty("requestId", out var requestIdEl) &&
                requestIdEl.TryGetInt32(out var requestId))
            {
                IpcResponse response;
                if (root.TryGetProperty("success", out var successEl) && successEl.GetBoolean())
                {
                    var data = root.TryGetProperty("data", out var dataEl) ? dataEl : (JsonElement?)null;
                    response = new IpcResponse { Success = true, Data = data };
                }
                else
                {
                    var error = root.TryGetProperty("error", out var errorEl)
                        ? errorEl.GetString() : "Unknown error";
                    response = new IpcResponse { Success = false, Error = error };
                }

                lock (_lock)
                {
                    if (_pendingRequests.TryGetValue(requestId, out var tcs))
                    {
                        tcs.TrySetResult(response);
                    }
                }
            }
            else if (root.TryGetProperty("eventType", out var eventTypeEl))
            {
                var eventType = eventTypeEl.GetString() ?? "unknown";
                var payload = root.TryGetProperty("payload", out var payloadEl) ? payloadEl : default;

                EventReceived?.Invoke(this, new IpcEventArgs
                {
                    EventType = eventType,
                    Payload = payload
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing incoming message: {Json}", json);
        }
    }

    private void SetConnected(bool connected)
    {
        if (_isConnected != connected)
        {
            _isConnected = connected;
            ConnectionStateChanged?.Invoke(this, connected);
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _hostCancellationToken = cancellationToken;
        try
        {
            await ConnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect during startup, will retry in background");
            _ = ReconnectAsync();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await DisconnectAsync();
    }

    public void Dispose()
    {
        DisconnectAsync().GetAwaiter().GetResult();
        _writeLock.Dispose();
    }
}
