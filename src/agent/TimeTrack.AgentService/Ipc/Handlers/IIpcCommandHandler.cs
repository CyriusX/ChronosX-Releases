namespace TimeTrack.AgentService.Ipc.Handlers;

/// <summary>
/// Interface for IPC command handlers (write operations)
/// </summary>
public interface IIpcCommandHandler
{
    /// <summary>
    /// The command name this handler processes (case-insensitive)
    /// </summary>
    string CommandName { get; }

    /// <summary>
    /// Handle the incoming command
    /// </summary>
    Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct);
}
