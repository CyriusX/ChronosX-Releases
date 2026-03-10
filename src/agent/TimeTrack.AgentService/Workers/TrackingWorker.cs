using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TimeTrack.AgentService.Configuration;
using TimeTrack.AgentService.Extensions;

namespace TimeTrack.AgentService.Workers;

/// <summary>
/// Worker principal de tracking que roda em background
/// </summary>
public sealed class TrackingWorker : BackgroundService
{
    private readonly ILogger<TrackingWorker> _logger;
    private readonly AgentSettings _settings;

    public TrackingWorker(
        ILogger<TrackingWorker> logger,
        AgentSettings settings)
    {
        _logger = logger;
        _settings = settings;

        // Configura prioridade do processo
        ServiceCollectionExtensions.ConfigureProcessPriority(_settings.ProcessPriority);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "TrackingWorker iniciando. PollingInterval: {PollingInterval}ms, IdleThreshold: {IdleThreshold}s",
            _settings.PollingIntervalMs,
            _settings.IdleThresholdSeconds);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ExecuteTrackingCycleAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Shutdown solicitado
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro no ciclo de tracking");
                }

                await Task.Delay(_settings.PollingIntervalMs, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }

        _logger.LogInformation("TrackingWorker encerrado");
    }

    private async Task ExecuteTrackingCycleAsync(CancellationToken cancellationToken)
    {
        // TODO: Implementar em CX-90 (RecordActiveWindow)
        // 1. Obter janela ativa via IActiveWindowProvider
        // 2. Verificar idle via IIdleDetector
        // 3. Registrar sessão ou idle period
        // 4. Atualizar estado

        _logger.LogDebug("Ciclo de tracking executado em {Time}", DateTime.UtcNow);

        await Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "TrackingWorker recebendo sinal de parada. Aguardando até {Timeout}s para graceful shutdown",
            _settings.GracefulShutdownTimeoutSeconds);

        // Cria um timeout para graceful shutdown
        using var timeoutCts = new CancellationTokenSource(
            TimeSpan.FromSeconds(_settings.GracefulShutdownTimeoutSeconds));

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCts.Token);

        try
        {
            await base.StopAsync(linkedCts.Token);
            _logger.LogInformation("TrackingWorker encerrado graciosamente");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "TrackingWorker encerrado por timeout após {Timeout}s",
                _settings.GracefulShutdownTimeoutSeconds);
        }
    }
}
