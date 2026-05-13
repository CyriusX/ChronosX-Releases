using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Services;

namespace TimeTrack.AgentService.Ipc.Handlers.Queries.Data;

/// <summary>
/// Proxies GET /api/v1/projects from the desktop UI through the agent.
/// Payload: { activeOnly?: bool } — defaults to true.
/// </summary>
public sealed class GetProjectsQueryHandler : IpcHandlerBase, IIpcQueryHandler
{
    private readonly IBackendTasksClient _backendTasks;
    private readonly ILogger<GetProjectsQueryHandler> _logger;

    public string QueryName => "GetProjects";

    public GetProjectsQueryHandler(IBackendTasksClient backendTasks, ILogger<GetProjectsQueryHandler> logger)
    {
        _backendTasks = backendTasks;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        var activeOnly = true;
        if (request.Payload.HasValue && request.Payload.Value.TryGetProperty("activeOnly", out var prop))
            activeOnly = prop.GetBoolean();

        var result = await _backendTasks.ListProjectsAsync(activeOnly, ct);
        var projects = (result?.Projects ?? []).Select(p => new
        {
            id = p.Id.ToString(),
            name = p.Name,
            color = p.Color,
            status = p.Status
        }).ToArray();
        return SuccessResponse(request.RequestId, projects);
    }
}
