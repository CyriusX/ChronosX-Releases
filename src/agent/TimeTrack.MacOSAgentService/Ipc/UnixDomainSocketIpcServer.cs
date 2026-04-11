using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TimeTrack.AgentService.Ipc;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.MacOSAgentService.Ipc;

public sealed class UnixDomainSocketIpcServer : BackgroundService, IIpcServer, IDisposable
{
    private const string DefaultSocketPath = "/var/tmp/TimeTrack.Agent.IPC";

    private readonly ILogger<UnixDomainSocketIpcServer> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string _socketPath;

    private Socket? _serverSocket;
    private Socket? _clientSocket;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private readonly object _writeLock = new();

    private bool _isListening;
    private bool _isClientConnected;

    public bool IsListening => _isListening;
    public bool IsClientConnected => _isClientConnected;

    public event EventHandler<IpcEvent>? EventReceived;
    public event EventHandler<bool>? ClientConnectionChanged;

    public UnixDomainSocketIpcServer(
        IServiceProvider serviceProvider,
        ILogger<UnixDomainSocketIpcServer> logger,
        string? socketPath = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _socketPath = socketPath ?? DefaultSocketPath;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Unix Domain Socket IPC Server on '{SocketPath}'", _socketPath);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await WaitForConnectionAsync(stoppingToken);
                await HandleClientAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in IPC server loop");
                await Task.Delay(1000, stoppingToken);
            }
        }

        _logger.LogInformation("Unix Domain Socket IPC Server stopped");
    }

    private async Task WaitForConnectionAsync(CancellationToken cancellationToken)
    {
        SetClientConnected(false);
        CleanupServer();

        if (File.Exists(_socketPath))
        {
            try { File.Delete(_socketPath); }
            catch (Exception ex) { _logger.LogWarning(ex, "Could not delete existing socket file"); }
        }

        _serverSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
        var endpoint = new UnixDomainSocketEndPoint(_socketPath);
        _serverSocket.Bind(endpoint);
        _serverSocket.Listen(1);

        try
        {
            File.SetUnixFileMode(_socketPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite |
                UnixFileMode.GroupRead | UnixFileMode.GroupWrite);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not set socket file permissions");
        }

        _isListening = true;
        _logger.LogInformation("IPC Server listening on {SocketPath}", _socketPath);

        _clientSocket = await _serverSocket.AcceptAsync(cancellationToken);

        _logger.LogDebug("Client connected, setting up streams...");

        try
        {
            var stream = new NetworkStream(_clientSocket);
            _reader = new StreamReader(stream, Encoding.UTF8);
            _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            SetClientConnected(true);
            _logger.LogInformation("DesktopHost connected via Unix Domain Socket");
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Client disconnected during stream setup");
            CleanupConnection();
            throw;
        }
    }

    private async Task HandleClientAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _reader != null)
            {
                var line = await _reader.ReadLineAsync(cancellationToken);
                if (line == null)
                {
                    _logger.LogWarning("Client disconnected");
                    break;
                }

                await ProcessMessageAsync(line, cancellationToken);
            }
        }
        catch (IOException ex)
        {
            _logger.LogDebug(ex, "Client connection lost");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling client messages");
        }
        finally
        {
            SetClientConnected(false);
            CleanupConnection();
        }
    }

    private async Task ProcessMessageAsync(string json, CancellationToken cancellationToken)
    {
        IpcRequest? request = null;

        try
        {
            request = JsonSerializer.Deserialize<IpcRequest>(json, _jsonOptions);
            if (request == null)
            {
                _logger.LogWarning("Received null request");
                return;
            }

            _logger.LogDebug("Received IPC {Type}: {Name} (RequestId: {RequestId})",
                request.Type, request.Name, request.RequestId);

            var router = _serviceProvider.GetRequiredService<IpcMessageRouter>();

            IpcResponse response;

            if (request.Type.Equals("command", StringComparison.OrdinalIgnoreCase))
            {
                response = await router.HandleCommandAsync(request, cancellationToken);
            }
            else if (request.Type.Equals("query", StringComparison.OrdinalIgnoreCase))
            {
                response = await router.HandleQueryAsync(request, cancellationToken);
            }
            else
            {
                response = new IpcResponse
                {
                    RequestId = request.RequestId,
                    Success = false,
                    Error = $"Unknown message type: {request.Type}"
                };
            }

            await SendResponseAsync(response, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse IPC message: {Json}", json);
            if (request != null)
            {
                await SendResponseAsync(new IpcResponse
                {
                    RequestId = request.RequestId,
                    Success = false,
                    Error = "Invalid JSON format"
                }, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing IPC message");
            if (request != null)
            {
                await SendResponseAsync(new IpcResponse
                {
                    RequestId = request.RequestId,
                    Success = false,
                    Error = ex.Message
                }, cancellationToken);
            }
        }
    }

    public Task SendEventAsync(IpcEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("SendEventAsync called: EventType={EventType}, IsClientConnected={IsClientConnected}",
            @event.EventType, IsClientConnected);

        if (!IsClientConnected || _writer == null)
        {
            _logger.LogWarning("Cannot send event {EventType} - no client connected",
                @event.EventType);
            return Task.CompletedTask;
        }

        try
        {
            var json = JsonSerializer.Serialize(new
            {
                eventType = @event.EventType,
                payload = @event.Payload
            }, _jsonOptions);

            lock (_writeLock)
            {
                _writer.WriteLine(json);
                _writer.Flush();
            }

            _logger.LogInformation("Event {EventType} sent successfully", @event.EventType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending event {EventType}", @event.EventType);
        }

        return Task.CompletedTask;
    }

    private Task SendResponseAsync(IpcResponse response, CancellationToken cancellationToken)
    {
        if (_writer == null)
        {
            _logger.LogWarning("SendResponseAsync: Writer is null");
            return Task.CompletedTask;
        }

        try
        {
            var json = JsonSerializer.Serialize(response, _jsonOptions);

            lock (_writeLock)
            {
                _writer.WriteLine(json);
                _writer.Flush();
            }

            _logger.LogDebug("Sent response for RequestId: {RequestId}", response.RequestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending response");
        }

        return Task.CompletedTask;
    }

    private void SetClientConnected(bool connected)
    {
        if (_isClientConnected != connected)
        {
            _isClientConnected = connected;
            ClientConnectionChanged?.Invoke(this, connected);
        }
    }

    private void CleanupConnection()
    {
        try
        {
            _writer?.Dispose();
            _reader?.Dispose();
            _clientSocket?.Dispose();
        }
        catch { }

        _writer = null;
        _reader = null;
        _clientSocket = null;
    }

    private void CleanupServer()
    {
        try
        {
            _serverSocket?.Dispose();
        }
        catch { }

        _serverSocket = null;
        _isListening = false;
    }

    public override void Dispose()
    {
        CleanupConnection();
        CleanupServer();

        try
        {
            if (File.Exists(_socketPath))
                File.Delete(_socketPath);
        }
        catch { }

        base.Dispose();
    }
}
