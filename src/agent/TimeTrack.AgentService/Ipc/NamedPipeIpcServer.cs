using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TimeTrack.AgentService.Ipc.Handlers;

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

        _pipeServer = CreateSecurePipeServer("TimeTrack.Agent.IPC");

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

    public async Task SendEventAsync(IpcEvent @event, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("SendEventAsync called: EventType={EventType}, IsClientConnected={IsClientConnected}",
            @event.EventType, IsClientConnected);

        if (!IsClientConnected || _writer == null)
        {
            _logger.LogWarning("Cannot send event {EventType} - no client connected (IsClientConnected={IsClientConnected}, Writer={Writer})",
                @event.EventType, IsClientConnected, _writer != null ? "not null" : "null");
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(new
            {
                eventType = @event.EventType,
                payload = @event.Payload
            }, _jsonOptions);

            _logger.LogInformation("SendEventAsync: Sending event JSON, length={Length}", json.Length);

            lock (_writeLock)
            {
                _writer.WriteLine(json);
                _writer.Flush();
            }

            _logger.LogInformation("SendEventAsync: Event {EventType} sent and flushed successfully", @event.EventType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending event {EventType}", @event.EventType);
        }
    }

    private async Task SendResponseAsync(IpcResponse response, CancellationToken cancellationToken)
    {
        if (_writer == null)
        {
            _logger.LogWarning("SendResponseAsync: Writer is null");
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(response, _jsonOptions);
            _logger.LogInformation("SendResponseAsync: Sending response for RequestId {RequestId}, Success: {Success}", response.RequestId, response.Success);

            lock (_writeLock)
            {
                _writer.WriteLine(json);
                _writer.Flush();
            }

            _logger.LogInformation("SendResponseAsync: Response sent and flushed");

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

    /// <summary>
    /// Creates the named pipe with explicit security so that:
    /// - Any authenticated user can connect (cross-session, cross-integrity-level)
    /// - A medium-integrity DesktopHost can connect to a high-integrity agent
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static NamedPipeServerStream CreateSecurePipeServer(string pipeName)
    {
        var security = new PipeSecurity();

        // Allow any authenticated user to read/write the pipe
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
            PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
            AccessControlType.Allow));

        // Full control for Administrators
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        var pipe = NamedPipeServerStreamAcl.Create(
            pipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            inBufferSize: 0,
            outBufferSize: 0,
            security);

        // Set mandatory integrity label to Low so a medium-integrity DesktopHost
        // can connect even when the agent runs elevated (high integrity).
        SetLowIntegrityLabel(pipe.SafePipeHandle);

        return pipe;
    }

    [SupportedOSPlatform("windows")]
    private static void SetLowIntegrityLabel(Microsoft.Win32.SafeHandles.SafePipeHandle handle)
    {
        // SDDL: SACL with low mandatory integrity label (NW = no write-up)
        const string lowIntegritySddl = "S:(ML;;NW;;;LW)";

        if (!ConvertStringSecurityDescriptorToSecurityDescriptor(
                lowIntegritySddl, 1, out IntPtr pSd, out _))
            return;

        try
        {
            if (GetSecurityDescriptorSacl(pSd, out bool present, out IntPtr sacl, out _) && present)
            {
                SetSecurityInfo(
                    handle.DangerousGetHandle(),
                    6,           // SE_KERNEL_OBJECT
                    0x00000010,  // LABEL_SECURITY_INFORMATION
                    IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, sacl);
            }
        }
        finally
        {
            LocalFree(pSd);
        }
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(
        string stringSd, uint revision, out IntPtr pSd, out uint sdSize);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetSecurityDescriptorSacl(
        IntPtr pSd, out bool present, out IntPtr sacl, out bool defaulted);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern int SetSecurityInfo(
        IntPtr handle, int objectType, uint si,
        IntPtr owner, IntPtr group, IntPtr dacl, IntPtr sacl);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);

    public override void Dispose()
    {
        CleanupConnection();
        base.Dispose();
    }
}
