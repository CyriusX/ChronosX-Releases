using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.Diagnostics;

public sealed class ReportExceptionCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "ReportException";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IExceptionReporter _exceptionReporter;
    private readonly ILogger<ReportExceptionCommandHandler> _logger;

    public ReportExceptionCommandHandler(
        IExceptionReporter exceptionReporter,
        ILogger<ReportExceptionCommandHandler> logger)
    {
        _exceptionReporter = exceptionReporter;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        if (!request.Payload.HasValue || request.Payload.Value.ValueKind != JsonValueKind.Object)
        {
            return ValidationErrorResponse(request.RequestId, "Payload is required");
        }

        ExceptionReport? report;
        try
        {
            report = request.Payload.Value.Deserialize<ExceptionReport>(JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize ReportException payload");
            return ValidationErrorResponse(request.RequestId, "Invalid payload");
        }

        if (report is null)
        {
            return ValidationErrorResponse(request.RequestId, "Invalid payload");
        }

        if (string.IsNullOrWhiteSpace(report.Component) ||
            string.IsNullOrWhiteSpace(report.Operation) ||
            string.IsNullOrWhiteSpace(report.ExceptionType))
        {
            return ValidationErrorResponse(request.RequestId, "Missing required fields");
        }

        if (string.Equals(report.ExceptionType, typeof(OperationCanceledException).FullName, StringComparison.Ordinal) ||
            string.Equals(report.ExceptionType, typeof(TaskCanceledException).FullName, StringComparison.Ordinal))
        {
            return SuccessResponse(request.RequestId, new { ignored = true });
        }

        await _exceptionReporter.ReportAsync(report, ct).ConfigureAwait(false);
        return SuccessResponse(request.RequestId, new { ok = true });
    }
}

