using System.Text.Json;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<IpcMessageRouter> _logger;

    public IpcMessageRouter(
        IEnumerable<IIpcCommandHandler> commands,
        IEnumerable<IIpcQueryHandler> queries,
        ILogger<IpcMessageRouter> logger)
    {
        _commands = commands.ToDictionary(
            c => c.CommandName.ToLowerInvariant(),
            StringComparer.OrdinalIgnoreCase);

        _queries = queries.ToDictionary(
            q => q.QueryName.ToLowerInvariant(),
            StringComparer.OrdinalIgnoreCase);

        _logger = logger;
    }

    public async Task<IpcResponse> HandleCommandAsync(IpcRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Routing command: {Name} (RequestId: {RequestId})", request.Name, request.RequestId);

        var normalizedName = request.Name.ToLowerInvariant();

        if (_commands.TryGetValue(normalizedName, out var handler))
        {
            _logger.LogInformation("Found handler for command: {Name}", request.Name);
            var response = await handler.HandleAsync(request, ct);
            _logger.LogInformation("Command {Name} completed: Success={Success}", request.Name, response.Success);
            return response;
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
            return await handler.HandleAsync(request, ct);
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
