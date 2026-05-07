using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Application.UseCases.IdleJustification;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.Tracking;

public sealed class DismissIdleJustificationCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "dismissIdleJustification";

    private readonly DismissIdleJustificationUseCase _useCase;
    private readonly ILogger<DismissIdleJustificationCommandHandler> _logger;

    public DismissIdleJustificationCommandHandler(
        DismissIdleJustificationUseCase useCase,
        ILogger<DismissIdleJustificationCommandHandler> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
            if (!request.Payload.HasValue || request.Payload.Value.ValueKind != JsonValueKind.Object)
                return ValidationErrorResponse(request.RequestId, "Payload is required");

            var payload = request.Payload.Value;
            if (!payload.TryGetProperty("idlePeriodId", out var idlePeriodIdProp) ||
                !Guid.TryParse(idlePeriodIdProp.GetString(), out var idlePeriodId))
                return ValidationErrorResponse(request.RequestId, "idlePeriodId is required");

            await _useCase.ExecuteAsync(new DismissIdleJustificationRequest
            {
                IdlePeriodId = idlePeriodId
            }, ct);

            return SuccessResponse(request.RequestId, new { idlePeriodId = idlePeriodId.ToString("D") });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error dismissing idle justification");
            return UnknownErrorResponse(request.RequestId, ex);
        }
    }
}
