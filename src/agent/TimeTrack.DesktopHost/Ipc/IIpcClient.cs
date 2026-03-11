using System.Text.Json;

namespace TimeTrack.DesktopHost.Ipc;

/// <summary>
/// Response from an IPC command
/// </summary>
public sealed class IpcResponse
{
    public bool Success { get; init; }
    public JsonElement? Data { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Event args for IPC events received from AgentService
/// </summary>
public sealed class IpcEventArgs : EventArgs
{
    public string EventType { get; init; } = string.Empty;
    public JsonElement Payload { get; init; }
}

/// <summary>
/// Interface for IPC communication with AgentService
/// </summary>
public interface IIpcClient
{
    /// <summary>
    /// Whether the client is connected to AgentService
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Event raised when an event is received from AgentService
    /// </summary>
    event EventHandler<IpcEventArgs>? EventReceived;

    /// <summary>
    /// Event raised when connection state changes
    /// </summary>
    event EventHandler<bool>? ConnectionStateChanged;

    /// <summary>
    /// Connect to AgentService via Named Pipe
    /// </summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnect from AgentService
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// Send a command to AgentService and wait for response
    /// </summary>
    Task<IpcResponse> SendCommandAsync(string command, object? payload = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Send a query to AgentService and wait for response
    /// </summary>
    Task<IpcResponse> SendQueryAsync(string query, object? payload = null, CancellationToken cancellationToken = default);
}
