using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Contracts.Services;

namespace TimeTrack.AgentService.Health;

/// <summary>
/// Health check do Agent que verifica estado do sync e tracking
/// </summary>
public sealed class AgentHealthCheck : IHealthCheck
{
    private readonly IOutboxRepository _outboxRepository;
    private readonly ISyncTransport _syncTransport;
    private readonly ILogger<AgentHealthCheck> _logger;

    public AgentHealthCheck(
        IOutboxRepository outboxRepository,
        ISyncTransport syncTransport,
        ILogger<AgentHealthCheck> logger)
    {
        _outboxRepository = outboxRepository;
        _syncTransport = syncTransport;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var data = new Dictionary<string, object>();

            // Verificar pending items no outbox
            var hasPending = await _outboxRepository.HasPendingItemsAsync(cancellationToken);
            data["has_pending_items"] = hasPending;

            // Verificar conectividade com backend
            bool backendHealthy;
            try
            {
                backendHealthy = await _syncTransport.CheckHealthAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Backend health check failed");
                backendHealthy = false;
            }

            data["backend_healthy"] = backendHealthy;

            // Determinar status
            if (!backendHealthy)
            {
                return HealthCheckResult.Unhealthy(
                    "Backend is not reachable",
                    data: data);
            }

            if (hasPending)
            {
                return HealthCheckResult.Degraded(
                    "Pending items in outbox",
                    data: data);
            }

            return HealthCheckResult.Healthy(
                "Agent is healthy",
                data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return HealthCheckResult.Unhealthy(
                "Health check failed",
                ex,
                null);
        }
    }
}
