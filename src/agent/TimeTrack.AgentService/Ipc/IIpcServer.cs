using System.Text.Json;

namespace TimeTrack.AgentService.Ipc;

/// <summary>
/// Request received from DesktopHost
/// </summary>
public sealed class IpcRequest
{
    public int RequestId { get; init; }
    public string Type { get; init; } = string.Empty; // "command" or "query"
    public string Name { get; init; } = string.Empty;
    public JsonElement? Payload { get; init; }
}

/// <summary>
/// Response sent back to DesktopHost
/// </summary>
public sealed class IpcResponse
{
    public int RequestId { get; init; }
    public bool Success { get; init; }
    public JsonElement? Data { get; init; }
    public string? Error { get; init; }
}

/// <summary>
/// Event pushed to DesktopHost
/// </summary>
public sealed class IpcEvent
{
    public string EventType { get; init; } = string.Empty;
    public object? Payload { get; init; }
}

/// <summary>
/// Interface for IPC Server that handles connections from DesktopHost
/// </summary>
public interface IIpcServer
{
    /// <summary>
    /// Whether the server is listening for connections
    /// </summary>
    bool IsListening { get; }

    /// <summary>
    /// Whether a client is currently connected
    /// </summary>
    bool IsClientConnected { get; }

    /// <summary>
    /// Send an event to connected clients
    /// </summary>
    Task SendEventAsync(IpcEvent @event, CancellationToken cancellationToken = default);
}
