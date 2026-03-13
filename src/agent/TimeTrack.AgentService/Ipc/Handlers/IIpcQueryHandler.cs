namespace TimeTrack.AgentService.Ipc.Handlers;

/// <summary>
/// Interface for IPC query handlers (read operations)
/// </summary>
public interface IIpcQueryHandler
{
    /// <summary>
    /// The query name this handler processes (case-insensitive)
    /// </summary>
    string QueryName { get; }

    /// <summary>
    /// Handle the incoming query
    /// </summary>
    Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct);
}
