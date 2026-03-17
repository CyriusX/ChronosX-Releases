using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Application.UseCases.RecordActiveWindow;
using TimeTrack.Agent.Application.UseCases.RecordIdlePeriod;
using TimeTrack.Agent.Application.UseCases.TrackingControl;
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
    private readonly RecordIdlePeriodUseCase _recordIdlePeriodUseCase;
    private readonly TrackingControlUseCase _trackingControl;

    private bool _isIdle = false;
    private DateTime? _idleStartedAt;

    public TrackingWorker(
        ILogger<TrackingWorker> logger,
        AgentSettings settings,
        IActiveWindowProvider activeWindowProvider,
        IIdleDetector idleDetector,
        ITrackingStateRepository stateRepository,
        ICurrentUserContext userContext,
        RecordActiveWindowUseCase recordActiveWindowUseCase,
        RecordIdlePeriodUseCase recordIdlePeriodUseCase,
        TrackingControlUseCase trackingControl)
    {
        _logger = logger;
        _settings = settings;
        _activeWindowProvider = activeWindowProvider;
        _idleDetector = idleDetector;
        _stateRepository = stateRepository;
        _userContext = userContext;
        _recordActiveWindowUseCase = recordActiveWindowUseCase;
        _recordIdlePeriodUseCase = recordIdlePeriodUseCase;
        _trackingControl = trackingControl;

        // Configura prioridade do processo
        ServiceCollectionExtensions.ConfigureProcessPriority(_settings.ProcessPriority);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "TrackingWorker iniciando. PollingInterval: {PollingInterval}ms, IdleThreshold: {IdleThreshold}s",
            _settings.PollingIntervalMs,
            _settings.IdleThresholdSeconds);

        // Subscribe to user authentication changes
        _userContext.UserChanged += OnUserChanged;

        // Try auto-resume on startup (if user is already authenticated)
        await EnsureTrackingActiveAsync(stoppingToken);

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

    /// <summary>
    /// Garante que o tracking está ativo no startup, resumindo automaticamente se estava pausado.
    /// </summary>
    private async Task EnsureTrackingActiveAsync(CancellationToken cancellationToken)
    {
        try
        {
            var userId = _userContext.UserId;
            if (userId == null)
            {
                _logger.LogDebug("Nenhum usuário autenticado. Auto-resume não executado.");
                return;
            }

            var state = await _stateRepository.GetAsync(userId.Value, cancellationToken);
            if (state != null && state.IsPaused)
            {
                _logger.LogInformation(
                    "Tracking estava pausado ({Status}). Retomando automaticamente...",
                    state.Status);

                await _trackingControl.ResumeAsync(new ResumeTrackingRequest
                {
                    ResumedBy = "AutoResume"
                }, cancellationToken);

                _logger.LogInformation("Tracking retomado automaticamente com sucesso.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao tentar retomar tracking automaticamente");
        }
    }

    /// <summary>
    /// Handler para quando o usuário é autenticado. Retoma o tracking se estava pausado.
    /// </summary>
    private void OnUserChanged(object? sender, UserChangedEventArgs e)
    {
        _logger.LogInformation(
            "Usuário mudou: {PreviousUserId} -> {NewUserId}",
            e.PreviousUserId,
            e.NewUserId);

        // If a new user is authenticated, try to auto-resume tracking
        if (e.NewUserId.HasValue)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await EnsureTrackingActiveAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro no auto-resume após mudança de usuário");
                }
            });
        }
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
                _idleStartedAt = DateTime.UtcNow.Subtract(idleThreshold);
                _logger.LogInformation(
                    "Usuário entrou em idle. Tempo de inatividade: {IdleTime}",
                    idleTime.Value);
            }
            return; // Não registra enquanto está idle
        }

        // 3. Se estava idle e retornou - salvar o período de inatividade
        if (_isIdle)
        {
            var idleEndedAt = DateTime.UtcNow;
            var idleDuration = idleEndedAt - _idleStartedAt;

            _logger.LogInformation(
                "Usuário retornou de idle após {Duration}. Salvando período...",
                idleDuration);

            // Salvar o período de idle no banco com sincronização
            try
            {
                await _recordIdlePeriodUseCase.ExecuteAsync(
                    new RecordIdlePeriodRequest
                    {
                        StartedAt = _idleStartedAt!.Value,
                        EndedAt = idleEndedAt,
                        ThresholdSeconds = _settings.IdleThresholdSeconds,
                        IsSystemDetected = true
                    },
                    cancellationToken);

                _logger.LogInformation(
                    "Período de idle salvo: {Duration:mm\\:ss}",
                    idleDuration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao salvar período de idle");
            }

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

        // Unsubscribe from events
        _userContext.UserChanged -= OnUserChanged;

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
