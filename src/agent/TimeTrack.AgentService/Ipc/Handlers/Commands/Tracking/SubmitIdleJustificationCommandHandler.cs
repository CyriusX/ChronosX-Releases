using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Application.UseCases.IdleJustification;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.Tracking;

public sealed class SubmitIdleJustificationCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "submitIdleJustification";

    private readonly SubmitIdleJustificationUseCase _useCase;
    private readonly ILogger<SubmitIdleJustificationCommandHandler> _logger;

    public SubmitIdleJustificationCommandHandler(
        SubmitIdleJustificationUseCase useCase,
        ILogger<SubmitIdleJustificationCommandHandler> logger)
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

            if (!payload.TryGetProperty("reasonCode", out var reasonCodeProp))
                return ValidationErrorResponse(request.RequestId, "reasonCode is required");

            var response = await _useCase.ExecuteAsync(new SubmitIdleJustificationRequest
            {
                IdlePeriodId = idlePeriodId,
                ReasonCode = reasonCodeProp.GetString() ?? string.Empty,
                Note = payload.TryGetProperty("note", out var noteProp) ? noteProp.GetString() : null
            }, ct);

            return SuccessResponse(request.RequestId, new
            {
                idlePeriodId = response.IdlePeriodId,
                reasonCode = response.ReasonCode,
                note = response.Note,
                submittedAtUtc = response.SubmittedAtUtc.ToString("O")
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting idle justification");
            return UnknownErrorResponse(request.RequestId, ex);
        }
    }
}
