using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.Settings;

/// <summary>
/// Handles setting work hours
/// </summary>
public sealed class SetWorkHoursCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "SetWorkHours";

    private readonly ILogger<SetWorkHoursCommandHandler> _logger;

    public SetWorkHoursCommandHandler(ILogger<SetWorkHoursCommandHandler> logger)
    {
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        // TODO: Implement work hours
        return SuccessResponse(request.RequestId, new { updated = true });
    }
}
