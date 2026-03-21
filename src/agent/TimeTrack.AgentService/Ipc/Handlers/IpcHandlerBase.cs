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

    /// <summary>
    /// Extracts an optional "date" property from the request payload.
    /// Returns DateTime.Today if no date is provided.
    /// </summary>
    protected static DateTime ExtractDateOrToday(IpcRequest request)
    {
        if (request.Payload.HasValue && request.Payload.Value.TryGetProperty("date", out var dateProp))
        {
            var dateStr = dateProp.GetString();
            if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, out var parsed))
                return parsed.Date;
        }
        return DateTime.Today;
    }
}
