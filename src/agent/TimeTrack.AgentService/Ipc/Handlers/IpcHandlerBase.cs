using System.Text.Json;

namespace TimeTrack.AgentService.Ipc.Handlers;

/// <summary>
/// Base class with common utilities for IPC handlers
/// </summary>
public abstract class IpcHandlerBase
{
    protected static IpcResponse SuccessResponse(int requestId, object data)
        => new()
        {
            RequestId = requestId,
            Success = true,
            Data = JsonSerializer.SerializeToElement(data)
        };

    protected static IpcResponse ErrorResponse(int requestId, string error)
        => new()
        {
            RequestId = requestId,
            Success = false,
            Error = error
        };

    protected static IpcResponse UnknownErrorResponse(int requestId, Exception ex)
        => new()
        {
            RequestId = requestId,
            Success = false,
            Error = ex.Message
        };
}
