using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.Sync;

/// <summary>
/// Redefine itens do outbox presos em backoff exponencial, tornando-os elegíveis para
/// a próxima tentativa de sync imediatamente. Usado para diagnóstico e recuperação manual.
/// </summary>
public sealed class ResetOutboxCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "ResetOutbox";

    private readonly IOutboxRepository _outboxRepository;
    private readonly ILogger<ResetOutboxCommandHandler> _logger;

    public ResetOutboxCommandHandler(
        IOutboxRepository outboxRepository,
        ILogger<ResetOutboxCommandHandler> logger)
    {
        _outboxRepository = outboxRepository;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
            var resetCount = await _outboxRepository.ResetStuckItemsAsync(ct);

            _logger.LogInformation(
                "ResetOutbox command executed: {Count} itens redefinidos para tentativa imediata.",
                resetCount);

            return SuccessResponse(request.RequestId, new
            {
                resetCount,
                message = $"{resetCount} outbox item(s) reset for immediate retry."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao executar ResetOutbox");
            return UnknownErrorResponse(request.RequestId, ex);
        }
    }
}
