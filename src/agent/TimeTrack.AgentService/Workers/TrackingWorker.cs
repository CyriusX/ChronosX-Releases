using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Application.UseCases.RecordActiveWindow;
using TimeTrack.Agent.Application.UseCases.IdleJustification;
using TimeTrack.Agent.Application.UseCases.RecordIdlePeriod;
using TimeTrack.Agent.Application.UseCases.TrackingControl;
using TimeTrack.Agent.Contracts.Providers;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Domain.Entities;
using TimeTrack.Agent.Domain.Enums;
using TimeTrack.Agent.Domain.ValueObjects;
using TimeTrack.AgentService.Configuration;
using TimeTrack.AgentService.Extensions;
using TimeTrack.AgentService.Ipc;

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
    private readonly ILocalSettingsRepository _localSettingsRepository;
    private readonly IIdlePeriodRepository _idlePeriodRepository;
    private readonly ICurrentUserContext _userContext;
    private readonly IOrgPolicyProvider _orgPolicyProvider;
    private readonly RecordActiveWindowUseCase _recordActiveWindowUseCase;
    private readonly RecordIdlePeriodUseCase _recordIdlePeriodUseCase;
    private readonly MarkIdleJustificationPendingUseCase _markIdleJustificationPendingUseCase;
    private readonly TrackingControlUseCase _trackingControl;
    private readonly IIpcServer _ipcServer;
    private readonly IHeartbeatService _heartbeatService;
    private readonly AgentStatusEventBroadcaster _statusBroadcaster;

    private bool _isIdle = false;
    private DateTime? _idleStartedAt;
    private Guid? _liveIdlePeriodId;
    private int? _liveIdleThresholdSeconds;

    // Sleep detection: track the wall-clock time of the last completed cycle.
    // TickCount64 freezes during sleep so Task.Delay returns immediately on wake,
    // but the wall-clock gap between cycles will be much larger than PollingIntervalMs.
    private DateTime _lastCycleUtc = DateTime.UtcNow;
    private const int SleepDetectionGapMs = 30_000; // Gap > 30s = assume sleep/resume

    // Diagnostics for "Top Folders" (File Explorer / Finder) extraction reliability.
    private int _folderFilePathMissingStreak;
    private string? _folderFilePathMissingAppKey;
    private bool _folderFilePathMissingLogged;

    // Diagnostics/UX: Automation permission required for Safari tab details (AppleScript).
    private DateTime _lastBrowserAutomationPermissionEventAtUtc = DateTime.MinValue;
    private string? _lastBrowserAutomationPermissionApp;

    public TrackingWorker(
        ILogger<TrackingWorker> logger,
        AgentSettings settings,
        IActiveWindowProvider activeWindowProvider,
        IIdleDetector idleDetector,
        ITrackingStateRepository stateRepository,
        ILocalSettingsRepository localSettingsRepository,
        IIdlePeriodRepository idlePeriodRepository,
        ICurrentUserContext userContext,
        IOrgPolicyProvider orgPolicyProvider,
        RecordActiveWindowUseCase recordActiveWindowUseCase,
        RecordIdlePeriodUseCase recordIdlePeriodUseCase,
        MarkIdleJustificationPendingUseCase markIdleJustificationPendingUseCase,
        TrackingControlUseCase trackingControl,
        IIpcServer ipcServer,
        IHeartbeatService heartbeatService,
        AgentStatusEventBroadcaster statusBroadcaster)
    {
        _logger = logger;
        _settings = settings;
        _activeWindowProvider = activeWindowProvider;
        _idleDetector = idleDetector;
        _stateRepository = stateRepository;
        _localSettingsRepository = localSettingsRepository;
        _idlePeriodRepository = idlePeriodRepository;
        _userContext = userContext;
        _orgPolicyProvider = orgPolicyProvider;
        _recordActiveWindowUseCase = recordActiveWindowUseCase;
        _recordIdlePeriodUseCase = recordIdlePeriodUseCase;
        _markIdleJustificationPendingUseCase = markIdleJustificationPendingUseCase;
        _trackingControl = trackingControl;
        _ipcServer = ipcServer;
        _heartbeatService = heartbeatService;
        _statusBroadcaster = statusBroadcaster;

        // Configura prioridade do processo
        ServiceCollectionExtensions.ConfigureProcessPriority(_settings.ProcessPriority);
    }

    private void TriggerImmediateHeartbeat()
    {
        try
        {
            var snapshot = new AgentHealthSnapshot(
                HealthStatus: _statusBroadcaster.CurrentHealthStatus,
                BackendReachable: _statusBroadcaster.CurrentBackendReachable,
                ConsecutiveSyncFailures: 0,
                LastSuccessfulSyncAt: null,
                IpcConnected: _ipcServer.IsClientConnected);

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            _ = _heartbeatService.SendHeartbeatAsync(snapshot, cts.Token)
                .ContinueWith(_ => cts.Dispose(), TaskScheduler.Default);
        }
        catch
        {
            // ignore
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "TrackingWorker iniciando. PollingInterval: {PollingInterval}ms, IdleThreshold: {IdleThreshold}s",
            _settings.PollingIntervalMs,
            _settings.IdleThresholdSeconds);

        // Subscribe to user authentication changes
        _userContext.UserChanged += OnUserChanged;

        // Ensure user context is initialized before we check tracking state.
        // The JwtCurrentUserContext constructor fires RefreshAsync as fire-and-forget;
        // awaiting it explicitly here prevents a race where UserId is still null.
        await _userContext.RefreshAsync(stoppingToken);

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
            if (state == null)
            {
                _logger.LogInformation(
                    "Nenhum estado de tracking encontrado para o usuário {UserId}. Iniciando automaticamente...",
                    userId.Value);

                await _trackingControl.StartAsync(new StartTrackingRequest
                {
                    StartedBy = "AutoStart"
                }, cancellationToken);

                _logger.LogInformation("Tracking iniciado automaticamente com sucesso.");
            }
            else if (state.IsPaused)
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
            else if (!state.IsActive)
            {
                _logger.LogInformation(
                    "Tracking estava desabilitado ({Status}). Reiniciando automaticamente...",
                    state.Status);

                await _trackingControl.StartAsync(new StartTrackingRequest
                {
                    StartedBy = "AutoRestart"
                }, cancellationToken);

                _logger.LogInformation("Tracking reiniciado automaticamente com sucesso.");
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

        // If a new user is authenticated, try to auto-resume tracking and notify the UI
        if (e.NewUserId.HasValue)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await EnsureTrackingActiveAsync(CancellationToken.None);

                    // Broadcast to the UI so it updates immediately without waiting for
                    // the next poll cycle. Without this, the UI stays frozen on the
                    // "paused" state it saw before tokens were received.
                    if (_ipcServer.IsClientConnected)
                    {
                        await _ipcServer.SendEventAsync(new IpcEvent
                        {
                            EventType = "trackingStateChanged",
                            Payload = new { isTracking = true, isPaused = false }
                        }, CancellationToken.None);
                    }
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
            return;
        }

        // 1. Verificar estado do tracking (filtrado por usuário)
        var state = await _stateRepository.GetAsync(userId.Value, cancellationToken);
        if (state == null)
        {
            _logger.LogDebug("Nenhum estado de tracking encontrado. Pulando ciclo.");
            return;
        }

        if (!state.IsActive)
        {
            _logger.LogDebug("Tracking não está ativo (Status: {Status}). Pulando ciclo.", state.Status);
            _lastCycleUtc = DateTime.UtcNow;
            return;
        }

        _logger.LogDebug("Tracking ativo. Executando ciclo de captura...");

        // Resolve effective idle threshold (org policy -> local override -> agent default)
        var localSettings = await _localSettingsRepository.GetAsync(cancellationToken);
        var orgIdleThreshold = await _orgPolicyProvider.GetIdleThresholdSecondsAsync(cancellationToken);
        var idleJustificationPromptThresholdSecs = await _orgPolicyProvider.GetIdleJustificationPromptThresholdSecondsAsync(cancellationToken);
        var effectiveIdleThresholdSecs = orgIdleThreshold ?? localSettings.IdleThresholdSeconds ?? _settings.IdleThresholdSeconds;
        var effectiveIdleThreshold = TimeSpan.FromSeconds(effectiveIdleThresholdSecs);

        // 2a. Sleep/wake detection — runs before idle check.
        // TickCount64 (and Task.Delay) freeze during system sleep. When the machine
        // wakes, the next cycle fires almost immediately but the wall-clock gap since
        // the last cycle equals the full sleep duration. Record that gap as idle time
        // so the absence shows up in the timeline, then reset idle state so normal
        // idle detection resumes cleanly from this point forward.
        var cycleNow = DateTime.UtcNow;
        var cycleGap = cycleNow - _lastCycleUtc;
        if (cycleGap.TotalMilliseconds > SleepDetectionGapMs)
        {
            _logger.LogInformation(
                "Wake from sleep/long pause detected: {Gap:g} gap since last cycle. Recording as idle period.",
                cycleGap);

            try
            {
                await _recordIdlePeriodUseCase.ExecuteAsync(
                    new RecordIdlePeriodRequest
                    {
                        StartedAt = _lastCycleUtc,
                        EndedAt = cycleNow,
                        ThresholdSeconds = effectiveIdleThresholdSecs,
                        IsSystemDetected = true
                    },
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record sleep idle period");
            }

            // Reset idle state so the first cycle after wake starts clean
            _isIdle = false;
            _idleStartedAt = null;
        }
        _lastCycleUtc = cycleNow;

        // 2. Verificar idle (org policy overrides local override)
        var idleTime = await _idleDetector.GetIdleTimeAsync(cancellationToken);

        if (idleTime.HasValue && idleTime.Value >= effectiveIdleThreshold)
        {
            var nowUtc = DateTime.UtcNow;
            if (!_isIdle)
            {
                _isIdle = true;
                // Clamp idle start: GetLastInputInfo() returns time since last system-wide
                // input, which can predate the current tracking session (e.g., if the user
                // started tracking after being away for hours). The idle period should never
                // start before the tracking state became active or before the idle threshold
                // window, whichever is later.
                var maxIdleStart = DateTime.UtcNow.Subtract(idleTime.Value);
                var trackingStart = state.UpdatedAt;
                _idleStartedAt = maxIdleStart < trackingStart ? trackingStart : maxIdleStart;
                _logger.LogInformation(
                    "Usuário entrou em idle. Tempo reportado: {IdleTime}, clamped start: {Start}",
                    idleTime.Value, _idleStartedAt);

                // Create a local-only placeholder idle period immediately so the dashboard
                // can show idle time in real time (it will be extended while idle).
                _liveIdlePeriodId = Guid.NewGuid();
                _liveIdleThresholdSeconds = effectiveIdleThresholdSecs;
                try
                {
                    var startUtc = _idleStartedAt.Value;
                    var placeholderEnd = startUtc.AddSeconds(1);
                    if (placeholderEnd <= startUtc)
                        placeholderEnd = startUtc.AddSeconds(1);

                    var placeholder = new IdlePeriod(
                        _liveIdlePeriodId.Value,
                        userId.Value,
                        new TimeRange(startUtc, placeholderEnd),
                        effectiveIdleThresholdSecs,
                        isSystemDetected: true);

                    await _idlePeriodRepository.SaveAsync(placeholder, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create live idle placeholder period");
                    _liveIdlePeriodId = null;
                    _liveIdleThresholdSeconds = null;
                }

                if (_ipcServer.IsClientConnected)
                {
                    try
                    {
                        await _ipcServer.SendEventAsync(new IpcEvent
                        {
                            EventType = "idleStateChanged",
                            Payload = new
                            {
                                isIdle = true,
                                idleTimeSeconds = (int)Math.Floor(idleTime.Value.TotalSeconds)
                            }
                        }, cancellationToken);
                    }
                    catch
                    {
                        // ignore push failures
                    }
                }

                // Also ping the backend immediately so web dashboards update without waiting for SyncWorker.
                TriggerImmediateHeartbeat();
            }
            else
            {
                // Extend the live placeholder while the user remains idle.
                if (_liveIdlePeriodId.HasValue && _idleStartedAt.HasValue && _liveIdleThresholdSeconds.HasValue)
                {
                    try
                    {
                        var startUtc = _idleStartedAt.Value;
                        var endUtc = nowUtc > startUtc ? nowUtc : startUtc.AddSeconds(1);
                        if (endUtc <= startUtc)
                            endUtc = startUtc.AddSeconds(1);

                        var live = new IdlePeriod(
                            _liveIdlePeriodId.Value,
                            userId.Value,
                            new TimeRange(startUtc, endUtc),
                            _liveIdleThresholdSeconds.Value,
                            isSystemDetected: true);

                        await _idlePeriodRepository.UpdateAsync(live, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to extend live idle placeholder period");
                    }
                }
            }
            // Don't record activity while idle — the idle period will be recorded
            // when the user returns. The current session naturally expires from
            // the 60s window, and a new session starts on return. This is correct:
            // idle time should NOT inflate session durations.
            return;
        }

        // 3. Se estava idle e retornou - salvar o período de inatividade
        if (_isIdle)
        {
            var idleEndedAt = DateTime.UtcNow;
            var idleDuration = idleEndedAt - _idleStartedAt!.Value;

            _logger.LogInformation(
                "Usuário retornou de idle após {Duration}. Salvando período...",
                idleDuration);

            // Salvar o período de idle no banco com sincronização
            try
            {
                var idleResult = await _recordIdlePeriodUseCase.ExecuteAsync(
                    new RecordIdlePeriodRequest
                    {
                        IdlePeriodId = _liveIdlePeriodId,
                        StartedAt = _idleStartedAt!.Value,
                        EndedAt = idleEndedAt,
                        ThresholdSeconds = effectiveIdleThresholdSecs,
                        IsSystemDetected = true,
                        CreateOutbox = true
                    },
                    cancellationToken);

                _logger.LogInformation(
                    "Período de idle salvo: {Duration:mm\\:ss}",
                    idleDuration);

                if (idleJustificationPromptThresholdSecs.HasValue &&
                    idleDuration.TotalSeconds >= idleJustificationPromptThresholdSecs.Value)
                {
                    await _markIdleJustificationPendingUseCase.ExecuteAsync(idleResult.IdlePeriodId, cancellationToken);

                    if (_ipcServer.IsClientConnected)
                    {
                        await _ipcServer.SendEventAsync(new IpcEvent
                        {
                            EventType = "showIdleJustificationPrompt",
                            Payload = new
                            {
                                idlePeriodId = idleResult.IdlePeriodId,
                                startedAt = _idleStartedAt!.Value.ToString("O"),
                                endedAt = idleEndedAt.ToString("O"),
                                durationSeconds = (int)idleDuration.TotalSeconds
                            }
                        }, cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao salvar período de idle");
            }

            _isIdle = false;
            _idleStartedAt = null;
            _liveIdlePeriodId = null;
            _liveIdleThresholdSeconds = null;

            if (_ipcServer.IsClientConnected)
            {
                try
                {
                    await _ipcServer.SendEventAsync(new IpcEvent
                    {
                        EventType = "idleStateChanged",
                        Payload = new { isIdle = false, idleTimeSeconds = 0 }
                    }, cancellationToken);
                }
                catch
                {
                    // ignore push failures
                }
            }

            // Also ping the backend immediately so web dashboards update without waiting for SyncWorker.
            TriggerImmediateHeartbeat();
        }

        // 4. Obter janela ativa
        var activeWindow = await _activeWindowProvider.GetActiveWindowAsync(cancellationToken);
        if (activeWindow == null)
        {
            // If we reached here, the idle detector (step 2) confirmed the user is NOT idle.
            // A null active window with an active user means the frontmost app is our own
            // DesktopHost (filtered by the provider). Don't enter idle state — just skip
            // this cycle. Real idle is already handled by step 2 above.
            _logger.LogDebug("No trackable active window (user is using our own app). Skipping cycle.");
            return;
        }

        TrackFolderExtractionBreadcrumb(activeWindow);
        await BroadcastBrowserAutomationPermissionIfNeededAsync(activeWindow, cancellationToken);

        // 5. Registrar atividade via Use Case
        var request = new RecordActiveWindowRequest
        {
            ExecutablePath = activeWindow.ExePath ?? activeWindow.ExePathHash,
            ApplicationName = activeWindow.DisplayName,
            WindowTitle = activeWindow.WindowTitle,
            FilePath = activeWindow.FilePath,
            BrowserUrl = activeWindow.BrowserUrl
        };

        await _recordActiveWindowUseCase.ExecuteAsync(request, cancellationToken);

        _logger.LogDebug(
            "Ciclo concluído: {App} - {Title}",
            activeWindow.DisplayName,
            activeWindow.WindowTitle ?? "sem título");
    }

    private void TrackFolderExtractionBreadcrumb(ActiveWindowInfo activeWindow)
    {
        var exePath = activeWindow.ExePath ?? string.Empty;
        if (string.IsNullOrWhiteSpace(exePath))
        {
            ResetFolderBreadcrumb();
            return;
        }

        var isExplorer = exePath.EndsWith("explorer.exe", StringComparison.OrdinalIgnoreCase);
        var isFinder = exePath.Contains("Finder.app", StringComparison.OrdinalIgnoreCase);
        if (!isExplorer && !isFinder)
        {
            ResetFolderBreadcrumb();
            return;
        }

        var appKey = exePath;

        if (!string.Equals(_folderFilePathMissingAppKey, appKey, StringComparison.OrdinalIgnoreCase))
        {
            _folderFilePathMissingAppKey = appKey;
            _folderFilePathMissingStreak = 0;
            _folderFilePathMissingLogged = false;
        }

        if (string.IsNullOrWhiteSpace(activeWindow.FilePath))
        {
            _folderFilePathMissingStreak++;

            // Once per streak: if we still can't extract a folder path after ~10 cycles,
            // Top Folders will show as empty; emit a breadcrumb to speed up diagnosis.
            if (!_folderFilePathMissingLogged && _folderFilePathMissingStreak >= 10)
            {
                _folderFilePathMissingLogged = true;
                _logger.LogWarning(
                    "TopFolders breadcrumb: active {App} window but FilePath extraction is empty for {Streak} cycles.",
                    isFinder ? "Finder" : "File Explorer",
                    _folderFilePathMissingStreak);
            }

            return;
        }

        ResetFolderBreadcrumb();
    }

    private async Task BroadcastBrowserAutomationPermissionIfNeededAsync(ActiveWindowInfo activeWindow, CancellationToken ct)
    {
        var app = activeWindow.BrowserAutomationPermissionRequiredForApp;
        if (string.IsNullOrWhiteSpace(app))
            return;

        if (!_ipcServer.IsClientConnected)
            return;

        // Rate limit: once per app per ~10 minutes.
        var now = DateTime.UtcNow;
        if (string.Equals(_lastBrowserAutomationPermissionApp, app, StringComparison.OrdinalIgnoreCase) &&
            (now - _lastBrowserAutomationPermissionEventAtUtc) < TimeSpan.FromMinutes(10))
        {
            return;
        }

        _lastBrowserAutomationPermissionApp = app;
        _lastBrowserAutomationPermissionEventAtUtc = now;

        try
        {
            await _ipcServer.SendEventAsync(new IpcEvent
            {
                EventType = "browserAutomationPermissionRequired",
                Payload = new { app }
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to broadcast browserAutomationPermissionRequired");
        }
    }

    private void ResetFolderBreadcrumb()
    {
        _folderFilePathMissingStreak = 0;
        _folderFilePathMissingAppKey = null;
        _folderFilePathMissingLogged = false;
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
