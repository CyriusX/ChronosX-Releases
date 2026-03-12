using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Application.UseCases.RecordActiveWindow;
using TimeTrack.Agent.Contracts.Providers;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Domain.Enums;
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
    private readonly IActiveWindowProvider _activeWindowProvider;
    private readonly IIdleDetector _idleDetector;
    private readonly ITrackingStateRepository _stateRepository;
    private readonly ICurrentUserContext _userContext;
    private readonly RecordActiveWindowUseCase _recordActiveWindowUseCase;

    private bool _isIdle = false;
    private DateTime? _idleStartedAt;

    public TrackingWorker(
        ILogger<TrackingWorker> logger,
        AgentSettings settings,
        IActiveWindowProvider activeWindowProvider,
        IIdleDetector idleDetector,
        ITrackingStateRepository stateRepository,
        ICurrentUserContext userContext,
        RecordActiveWindowUseCase recordActiveWindowUseCase)
    {
        _logger = logger;
        _settings = settings;
        _activeWindowProvider = activeWindowProvider;
        _idleDetector = idleDetector;
        _stateRepository = stateRepository;
        _userContext = userContext;
        _recordActiveWindowUseCase = recordActiveWindowUseCase;

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
        // 0. Verificar se há usuário autenticado
        var userId = _userContext.UserId;
        if (userId == null)
        {
            _logger.LogDebug("Nenhum usuário autenticado. Pulando ciclo de tracking.");
            return;
        }

        // 1. Verificar estado do tracking (filtrado por usuário)
        var state = await _stateRepository.GetAsync(userId.Value, cancellationToken);
        if (state == null)
        {
            _logger.LogInformation("Nenhum estado de tracking encontrado. Pulando ciclo.");
            return;
        }

        if (!state.IsActive)
        {
            _logger.LogInformation("Tracking não está ativo (Status: {Status}). Pulando ciclo.", state.Status);
            return;
        }

        _logger.LogDebug("Tracking ativo. Executando ciclo de captura...");

        // 2. Verificar idle
        var idleTime = await _idleDetector.GetIdleTimeAsync(cancellationToken);
        var idleThreshold = TimeSpan.FromSeconds(_settings.IdleThresholdSeconds);

        if (idleTime.HasValue && idleTime.Value >= idleThreshold)
        {
            if (!_isIdle)
            {
                _isIdle = true;
                _idleStartedAt = DateTime.UtcNow;
                _logger.LogInformation(
                    "Usuário entrou em idle. Tempo de inatividade: {IdleTime}",
                    idleTime.Value);
            }
            return; // Não registra enquanto está idle
        }

        // 3. Se estava idle e retornou
        if (_isIdle)
        {
            var idleDuration = DateTime.UtcNow - _idleStartedAt;
            _logger.LogInformation(
                "Usuário retornou de idle após {Duration}",
                idleDuration);
            _isIdle = false;
            _idleStartedAt = null;
        }

        // 4. Obter janela ativa
        var activeWindow = await _activeWindowProvider.GetActiveWindowAsync(cancellationToken);
        if (activeWindow == null)
        {
            _logger.LogDebug("Nenhuma janela ativa detectada");
            return;
        }

        // 5. Registrar atividade via Use Case
        var request = new RecordActiveWindowRequest
        {
            ExecutablePath = activeWindow.ExePath ?? activeWindow.ExePathHash,
            ApplicationName = activeWindow.DisplayName,
            WindowTitle = activeWindow.WindowTitle
        };

        await _recordActiveWindowUseCase.ExecuteAsync(request, cancellationToken);

        _logger.LogDebug(
            "Ciclo concluído: {App} - {Title}",
            activeWindow.DisplayName,
            activeWindow.WindowTitle ?? "sem título");
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
