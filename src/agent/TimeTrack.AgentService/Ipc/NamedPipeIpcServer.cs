using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TimeTrack.AgentService.Ipc;

/// <summary>
/// Named Pipe Server that accepts connections from DesktopHost
/// Implements the server side of the IPC communication
/// </summary>
public sealed class NamedPipeIpcServer : BackgroundService, IIpcServer, IDisposable
{
    private readonly ILogger<NamedPipeIpcServer> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly JsonSerializerOptions _jsonOptions;

    private NamedPipeServerStream? _pipeServer;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private readonly object _writeLock = new();

    private bool _isListening;
    private bool _isClientConnected;

    public bool IsListening => _isListening;
    public bool IsClientConnected => _isClientConnected;

    public event EventHandler<IpcEvent>? EventReceived;
    public event EventHandler<bool>? ClientConnectionChanged;

    public NamedPipeIpcServer(
        IServiceProvider serviceProvider,
        ILogger<NamedPipeIpcServer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Named Pipe IPC Server on 'TimeTrack.Agent.IPC'");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await WaitForConnectionAsync(stoppingToken);
                await HandleClientAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in IPC server loop");
                await Task.Delay(1000, stoppingToken);
            }
        }

        _logger.LogInformation("Named Pipe IPC Server stopped");
    }

    private async Task WaitForConnectionAsync(CancellationToken cancellationToken)
    {
        SetClientConnected(false);
        _isListening = true;

        _pipeServer = new NamedPipeServerStream(
            "TimeTrack.Agent.IPC",
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        _logger.LogDebug("Waiting for DesktopHost connection...");

        await _pipeServer.WaitForConnectionAsync(cancellationToken);

        _logger.LogDebug("Client connected, setting up streams...");

        try
        {
            _reader = new StreamReader(_pipeServer, Encoding.UTF8);
            _writer = new StreamWriter(_pipeServer, Encoding.UTF8);

            SetClientConnected(true);
            _logger.LogInformation("DesktopHost connected via Named Pipe");
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

            using var scope = _serviceProvider.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<IpcMessageHandler>();

            IpcResponse response;

            if (request.Type.Equals("command", StringComparison.OrdinalIgnoreCase))
            {
                response = await handler.HandleCommandAsync(request, cancellationToken);
            }
            else if (request.Type.Equals("query", StringComparison.OrdinalIgnoreCase))
            {
                response = await handler.HandleQueryAsync(request, cancellationToken);
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

    public async Task SendEventAsync(IpcEvent @event, CancellationToken cancellationToken = default)
    {
        if (!IsClientConnected || _writer == null)
        {
            _logger.LogDebug("Cannot send event - no client connected");
            return;
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

            _logger.LogDebug("Sent event: {EventType}", @event.EventType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending event");
        }
    }

    private async Task SendResponseAsync(IpcResponse response, CancellationToken cancellationToken)
    {
        if (_writer == null) return;

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
            _pipeServer?.Dispose();
        }
        catch { }

        _writer = null;
        _reader = null;
        _pipeServer = null;
    }

    public override void Dispose()
    {
        CleanupConnection();
        base.Dispose();
    }
}
