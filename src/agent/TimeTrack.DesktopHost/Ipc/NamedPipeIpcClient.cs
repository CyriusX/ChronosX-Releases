using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TimeTrack.DesktopHost.Configuration;

namespace TimeTrack.DesktopHost.Ipc;

/// <summary>
/// IPC Client implementation using Named Pipes
/// Communicates with AgentService which acts as the Named Pipe Server
/// </summary>
public sealed class NamedPipeIpcClient : IIpcClient, IpcClientHostedService, IDisposable
{
    private readonly DesktopHostSettings _settings;
    private readonly ILogger<NamedPipeIpcClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    private NamedPipeClientStream? _pipeClient;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private CancellationTokenSource? _listenCts;
    private Task? _listenTask;
    private int _requestId;
    private readonly Dictionary<int, TaskCompletionSource<IpcResponse>> _pendingRequests = new();
    private readonly object _lock = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private bool _isConnected;
    public bool IsConnected => _isConnected;

    public event EventHandler<IpcEventArgs>? EventReceived;
    public event EventHandler<bool>? ConnectionStateChanged;

    public NamedPipeIpcClient(DesktopHostSettings settings, ILogger<NamedPipeIpcClient> logger)
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
                _logger.LogDebug("Attempting to connect to pipe '{PipeName}' (attempt {Attempt}/{Max})",
                    _settings.PipeName, attempts + 1, maxAttempts);

                _pipeClient = new NamedPipeClientStream(
                    ".",
                    _settings.PipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);

                await _pipeClient.ConnectAsync(_settings.ConnectionTimeoutMs, cancellationToken);

                _reader = new StreamReader(_pipeClient, Encoding.UTF8);
                _writer = new StreamWriter(_pipeClient, Encoding.UTF8) { AutoFlush = true };

                SetConnected(true);
                StartListening();

                _logger.LogInformation("Connected to AgentService via Named Pipe '{PipeName}'", _settings.PipeName);
                return;
            }
            catch (Exception ex)
            {
                attempts++;
                _logger.LogWarning(ex, "Failed to connect to AgentService (attempt {Attempt}/{Max})",
                    attempts, maxAttempts);

                if (attempts < maxAttempts)
                {
                    await Task.Delay(_settings.ReconnectionDelayMs, cancellationToken);
                }
            }
        }

        throw new InvalidOperationException($"Failed to connect to AgentService after {maxAttempts} attempts");
    }

    public async Task DisconnectAsync()
    {
        StopListening();
        SetConnected(false);

        try
        {
            _writer?.Dispose();
            _reader?.Dispose();
            _pipeClient?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during disconnect");
        }

        _writer = null;
        _reader = null;
        _pipeClient = null;

        _logger.LogInformation("Disconnected from AgentService");
    }

    public async Task<IpcResponse> SendCommandAsync(string command, object? payload = null, CancellationToken cancellationToken = default)
    {
        return await SendMessageAsync("command", command, payload, cancellationToken);
    }

    public async Task<IpcResponse> SendQueryAsync(string query, object? payload = null, CancellationToken cancellationToken = default)
    {
        return await SendMessageAsync("query", query, payload, cancellationToken);
    }

    private async Task<IpcResponse> SendMessageAsync(string type, string name, object? payload, CancellationToken cancellationToken)
    {
        _logger.LogInformation("SendMessageAsync called: Type={Type}, Name={Name}, IsConnected={IsConnected}", type, name, IsConnected);

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

            _logger.LogInformation("Waiting for write lock...");
            await _writeLock.WaitAsync(cancellationToken);
            _logger.LogInformation("Write lock acquired");
            try
            {
                await _writer!.WriteLineAsync(json);
                await _writer.FlushAsync(cancellationToken);
                _logger.LogInformation("Message sent and flushed");
            }
            finally
            {
                _writeLock.Release();
            }

            // Wait for response with timeout
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
                    _logger.LogWarning("Pipe closed by server");
                    SetConnected(false);
                    break;
                }

                _logger.LogInformation("ListenAsync: Received message: {Message}", line.Length > 200 ? line[..200] + "..." : line);
                ProcessIncomingMessage(line);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in listen loop");
            SetConnected(false);
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
                // This is a response to a pending request
                IpcResponse response;
                if (root.TryGetProperty("success", out var successEl) && successEl.GetBoolean())
                {
                    var data = root.TryGetProperty("data", out var dataEl) ? dataEl : (JsonElement?)null;
                    response = new IpcResponse { Success = true, Data = data };
                }
                else
                {
                    var error = root.TryGetProperty("error", out var errorEl) ? errorEl.GetString() : "Unknown error";
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
                // This is an event from AgentService
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

    // IHostedService implementation
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await ConnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect during startup");
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

/// <summary>
/// Marker interface for IpcClient to be registered as IHostedService
/// </summary>
public interface IpcClientHostedService : IHostedService
{
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync();
}
