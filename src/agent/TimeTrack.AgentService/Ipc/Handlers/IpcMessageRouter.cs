using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc;

/// <summary>
/// Routes IPC messages to appropriate handlers
///
/// SRP: Apenas roteamento de/// OCP: Novos handlers via DI
/// DIP: Depende de handlers (abstrações)
/// </summary>
public sealed class IpcMessageRouter
{
    private readonly IReadOnlyDictionary<string, IIpcCommandHandler> _commands;
    private readonly IReadOnlyDictionary<string, IIpcQueryHandler> _queries;
    private readonly IExceptionReporter _exceptionReporter;
    private readonly ILogger<IpcMessageRouter> _logger;

    public IpcMessageRouter(
        IEnumerable<IIpcCommandHandler> commands,
        IEnumerable<IIpcQueryHandler> queries,
        IExceptionReporter exceptionReporter,
        ILogger<IpcMessageRouter> logger)
    {
        _commands = commands.ToDictionary(
            c => c.CommandName.ToLowerInvariant(),
            StringComparer.OrdinalIgnoreCase);

        _queries = queries.ToDictionary(
            q => q.QueryName.ToLowerInvariant(),
            StringComparer.OrdinalIgnoreCase);

        _exceptionReporter = exceptionReporter;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleCommandAsync(IpcRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Routing command: {Name} (RequestId: {RequestId})", request.Name, request.RequestId);

        var normalizedName = request.Name.ToLowerInvariant();

        if (_commands.TryGetValue(normalizedName, out var handler))
        {
            _logger.LogInformation("Found handler for command: {Name}", request.Name);
            try
            {
                var response = await handler.HandleAsync(request, ct).ConfigureAwait(false);
                _logger.LogInformation("Command {Name} completed: Success={Success}", request.Name, response.Success);
                return response;
            }
            catch (Exception ex) when (ExceptionReport.IsCancellation(ex))
            {
                _logger.LogDebug(ex, "Command {Name} canceled", request.Name);
                return new IpcResponse
                {
                    RequestId = request.RequestId,
                    Success = false,
                    Error = "Request canceled"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in command handler: {Name}", request.Name);
                try
                {
                    var report = ExceptionReport.FromException(
                        ex,
                        component: "AgentService",
                        operation: $"ipc.command.{request.Name}",
                        context: new Dictionary<string, string?>
                        {
                            ["requestId"] = request.RequestId.ToString(),
                            ["name"] = request.Name
                        });
                    await _exceptionReporter.ReportAsync(report, CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // never throw from router
                }

                return new IpcResponse
                {
                    RequestId = request.RequestId,
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        _logger.LogWarning("Unknown command: {Name}", request.Name);
        return new IpcResponse
        {
            RequestId = request.RequestId,
            Success = false,
            Error = $"Unknown command: {request.Name}",
            Data = JsonSerializer.SerializeToElement(new { error = $"Unknown command: {request.Name}" })
        };
    }

    public async Task<IpcResponse> HandleQueryAsync(IpcRequest request, CancellationToken ct)
    {
        _logger.LogDebug("Routing query: {Name}", request.Name);

        var normalizedName = request.Name.ToLowerInvariant();

        if (_queries.TryGetValue(normalizedName, out var handler))
        {
            try
            {
                return await handler.HandleAsync(request, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ExceptionReport.IsCancellation(ex))
            {
                _logger.LogDebug(ex, "Query {Name} canceled", request.Name);
                return new IpcResponse
                {
                    RequestId = request.RequestId,
                    Success = false,
                    Error = "Request canceled"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in query handler: {Name}", request.Name);
                try
                {
                    var report = ExceptionReport.FromException(
                        ex,
                        component: "AgentService",
                        operation: $"ipc.query.{request.Name}",
                        context: new Dictionary<string, string?>
                        {
                            ["requestId"] = request.RequestId.ToString(),
                            ["name"] = request.Name
                        });
                    await _exceptionReporter.ReportAsync(report, CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // never throw from router
                }

                return new IpcResponse
                {
                    RequestId = request.RequestId,
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        _logger.LogWarning("Unknown query: {Name}", request.Name);
        return new IpcResponse
        {
            RequestId = request.RequestId,
            Success = false,
            Error = $"Unknown query: {request.Name}",
            Data = JsonSerializer.SerializeToElement(new { error = $"Unknown query: {request.Name}" })
        };
    }
}
