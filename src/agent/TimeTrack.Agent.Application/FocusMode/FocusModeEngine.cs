using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Notifications;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Domain.Entities;
using TimeTrack.Agent.Domain.Enums;
using TimeTrack.Agent.Domain.ValueObjects;

namespace TimeTrack.Agent.Application.FocusMode;

/// <summary>
/// FocusModeEngine - Motor de gerenciamento de ciclos de foco
///
/// SOLID:
/// - SRP: Apenas gerencia a máquina de estados do modo de foco
/// - OCP: Extensível para novos modos de foco via polimorfismo
/// - LSP: Implementa IFocusModeEngine corretamente
/// - ISP: Implementa apenas métodos de ciclo de foco
/// - DIP: Depende de abstrações (IFocusModeEngine, INotificationService)
///
/// Composition:
/// - Usa PeriodicTimer para contagem regressiva sem drift
/// - Usa INotificationService para notificar transições
/// </summary>
public sealed class FocusModeEngine : IFocusModeEngine, IDisposable
{
    private readonly ILogger<FocusModeEngine> _logger;
    private readonly INotificationService _notificationService;
    private readonly ICurrentUserContext _userContext;
    private readonly IFocusCycleRepository _focusCycleRepository;
    private readonly IOutboxRepository _outboxRepository;

    private FocusModeState _state = FocusModeState.Off;
    private FocusModePolicy _policy = FocusModePolicy.Disabled;
    private CancellationTokenSource? _cts;
    private PeriodicTimer? _timer;

    // Current cycle tracking
    private FocusCycle? _currentCycle;
    private int _currentCycleNumber = 0;
    private int _completedCyclesToday = 0;
    private int _remainingMs = 0;
    private DateTime? _cycleStartedAt;
    private int _plannedDurationMs = 0;
    private BreakType _nextBreakType = BreakType.Short;

    // State tracking
    private bool _disposed;

    public event EventHandler<FocusModeStateChangedEventArgs>? StateChanged;

    public FocusModeSnapshot CurrentSnapshot => GetSnapshot();

    public FocusModeEngine(
        ILogger<FocusModeEngine> logger,
        INotificationService notificationService,
        ICurrentUserContext userContext,
        IFocusCycleRepository focusCycleRepository,
        IOutboxRepository outboxRepository)
    {
        _logger = logger;
        _notificationService = notificationService;
        _userContext = userContext;
        _focusCycleRepository = focusCycleRepository;
        _outboxRepository = outboxRepository;
    }

    /// <summary>
    /// Aplica uma nova política de modo de foco
    /// </summary>
    public void ApplyPolicy(FocusModePolicy policy)
    {
        _logger.LogInformation(
            "Applying focus mode policy: Enabled={Enabled}, Mode={Mode}",
            policy.Enabled,
            policy.Mode);

        _policy = policy;

        if (!policy.Enabled)
        {
            Stop();
            return;
        }

        // Se não permite override, inicia automaticamente
        if (!policy.AllowUserOverride && _state == FocusModeState.Off)
        {
            Start();
        }
    }

    /// <summary>
    /// Inicia o motor de modo de foco
    /// </summary>
    public bool Start()
    {
        if (_policy is { Enabled: false } or { Mode: FocusModeType.None })
        {
            _logger.LogWarning("Cannot start: policy disabled or mode is none");
            return false;
        }

        if (_state == FocusModeState.FocusRunning || _state == FocusModeState.BreakRunning)
        {
            _logger.LogWarning("Cannot start: already running");
            return false;
        }

        _logger.LogInformation("Starting focus mode: {Mode}", _policy.Mode);

        // Inicia novo ciclo de foco
        StartFocusCycle();

        return true;
    }

    /// <summary>
    /// Pausa o ciclo atual
    /// </summary>
    public bool Pause()
    {
        if (_state != FocusModeState.FocusRunning && _state != FocusModeState.BreakRunning)
        {
            _logger.LogWarning("Cannot pause: not running");
            return false;
        }

        _logger.LogInformation("Pausing focus mode");

        var previousState = _state;
        _state = FocusModeState.FocusPaused;
        _timer?.Dispose();
        _timer = null;

        OnStateChanged(previousState, _state, "User paused");

        return true;
    }

    /// <summary>
    /// Retoma o ciclo pausado
    /// </summary>
    public bool Resume()
    {
        if (_state != FocusModeState.FocusPaused)
        {
            _logger.LogWarning("Cannot resume: not paused");
            return false;
        }

        _logger.LogInformation("Resuming focus mode");

        var previousState = _state;
        _state = FocusModeState.FocusRunning;

        // Retoma contagem de onde parou
        _ = StartTimerAsync();

        OnStateChanged(previousState, _state, "User resumed");

        return true;
    }

    /// <summary>
    /// Para completamente o motor
    /// </summary>
    public void Stop()
    {
        if (_state == FocusModeState.Off)
            return;

        _logger.LogInformation("Stopping focus mode");

        var previousState = _state;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        _timer?.Dispose();
        _timer = null;

        _state = FocusModeState.Off;
        _remainingMs = 0;
        _cycleStartedAt = null;
        _currentCycleNumber = 0;

        OnStateChanged(previousState, FocusModeState.Off, "Stopped");
    }

    /// <summary>
    /// Pula a pausa atual
    /// </summary>
    public bool SkipBreak()
    {
        if (_state != FocusModeState.BreakRunning)
        {
            _logger.LogWarning("Cannot skip break: not in break");
            return false;
        }

        _logger.LogInformation("Skipping break");

        _timer?.Dispose();
        _timer = null;

        StartFocusCycle();

        return true;
    }

    /// <summary>
    /// Obtém o snapshot atual
    /// </summary>
    public FocusModeSnapshot GetSnapshot()
    {
        return new FocusModeSnapshot
        {
            State = _state,
            Mode = _policy.Mode,
            RemainingMs = _remainingMs,
            CycleNumber = _currentCycleNumber,
            TotalCyclesToday = _completedCyclesToday,
            NextBreakType = _nextBreakType,
            CycleStartedAt = _cycleStartedAt,
            PlannedDurationMs = _plannedDurationMs,
            AllowUserOverride = _policy.AllowUserOverride,
            Timestamp = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Obtém a política atual
    /// </summary>
    public FocusModePolicy GetPolicy() => _policy;

    // ========================================================================
    // PRIVATE METHODS
    // ========================================================================

    private void StartFocusCycle()
    {
        _currentCycleNumber++;
        _cycleStartedAt = DateTime.UtcNow;

        var focusMinutes = _policy.GetFocusMinutes();
        _plannedDurationMs = focusMinutes * 60 * 1000;
        _remainingMs = _plannedDurationMs;

        var previousState = _state;
        _state = FocusModeState.FocusRunning;

        _logger.LogInformation(
            "Starting focus cycle #{Cycle}: {Minutes}min",
            _currentCycleNumber,
            focusMinutes);

        // Create and save FocusCycle entity
        _ = CreateFocusCycleAsync(focusMinutes);

        _ = StartTimerAsync();

        OnStateChanged(previousState, _state, "Focus cycle started");

        // Notifica início se permitido
        if (_policy.AllowUserOverride)
        {
            _ = NotifyFocusStartAsync();
        }
    }

    private async Task CreateFocusCycleAsync(int focusMinutes)
    {
        var userId = _userContext.UserId;
        if (!userId.HasValue)
        {
            _logger.LogWarning("Cannot create focus cycle: user not authenticated");
            return;
        }

        try
        {
            _currentCycle = new FocusCycle(
                Guid.NewGuid(),
                userId.Value,
                _policy.Mode,
                _currentCycleNumber,
                focusMinutes * 60 * 1000);

            await _focusCycleRepository.SaveAsync(_currentCycle);
            _logger.LogDebug("Focus cycle created: {CycleId}", _currentCycle.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create focus cycle");
            _currentCycle = null;
        }
    }

    private void StartBreakCycle()
    {
        _cycleStartedAt = DateTime.UtcNow;

        var breakMinutes = _policy.GetBreakMinutes(_nextBreakType);
        _plannedDurationMs = breakMinutes * 60 * 1000;
        _remainingMs = _plannedDurationMs;

        var previousState = _state;
        _state = FocusModeState.BreakRunning;

        _logger.LogInformation(
            "Starting {BreakType} break: {Minutes}min",
            _nextBreakType,
            breakMinutes);

        _ = StartTimerAsync();
        _ = NotifyBreakStartAsync();

        OnStateChanged(previousState, _state, "Break started");
    }

    private async Task StartTimerAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        _timer?.Dispose();
        _timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        try
        {
            while (await _timer.WaitForNextTickAsync(_cts.Token))
            {
                _remainingMs -= 1000;

                // Notifica atualização a cada 30s
                if (_remainingMs % 30000 == 0)
                {
                    OnStateChanged(_state, _state, "Tick");
                }

                if (_remainingMs <= 0)
                {
                    await OnTimerExpiredAsync();
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal quando pausado ou parado
        }
    }

    private async Task OnTimerExpiredAsync()
    {
        _logger.LogInformation("Timer expired in state {State}", _state);

        if (_state == FocusModeState.FocusRunning)
        {
            _completedCyclesToday++;

            // Complete and persist the focus cycle
            await CompleteAndSyncFocusCycleAsync();

            // Marca ciclo como completado
            await NotifyFocusEndAsync();

            // Calcula próxima pausa
            if (_policy.Mode == FocusModeType.Pomodoro)
            {
                var cyclesBeforeLong = _policy.GetCyclesBeforeLongBreak();
                _nextBreakType = (_currentCycleNumber % cyclesBeforeLong == 0)
                    ? BreakType.Long
                    : BreakType.Short;
            }

            StartBreakCycle();
        }
        else if (_state == FocusModeState.BreakRunning)
        {
            await NotifyBreakEndAsync();
            StartFocusCycle();
        }
    }

    private async Task CompleteAndSyncFocusCycleAsync()
    {
        if (_currentCycle == null)
        {
            _logger.LogWarning("No current focus cycle to complete");
            return;
        }

        try
        {
            // Complete the cycle
            _currentCycle.Complete();

            // Save to local repository
            await _focusCycleRepository.SaveAsync(_currentCycle);

            // Create outbox item for sync
            var outboxItem = CreateFocusSessionOutboxItem(_currentCycle);
            await _outboxRepository.AddAsync(outboxItem);

            _logger.LogInformation(
                "Focus cycle completed and queued for sync: {CycleId}, Duration: {Duration}ms",
                _currentCycle.Id,
                _currentCycle.ActualMs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete focus cycle");
        }
        finally
        {
            _currentCycle = null;
        }
    }

    private OutboxItem CreateFocusSessionOutboxItem(FocusCycle cycle)
    {
        var status = cycle.Completed ? "Completed" : "Cancelled";
        var actualMinutes = cycle.ActualMs.HasValue ? cycle.ActualMs.Value / 60000 : (int?)null;
        var plannedMinutes = cycle.PlannedMs / 60000;

        var payload = new
        {
            Id = cycle.Id,
            StartedAt = cycle.StartedAt,
            EndedAt = cycle.EndedAt,
            PlannedDurationMinutes = plannedMinutes,
            ActualDurationMinutes = actualMinutes,
            Status = status,
            FocusScore = CalculateFocusScore(cycle)
        };

        var payloadJson = JsonSerializer.Serialize(payload);
        var idempotencyKey = GenerateIdempotencyKey(cycle.Id, cycle.StartedAt);

        return OutboxItem.Create(
            "focus_session",
            cycle.Id,
            payloadJson,
            idempotencyKey);
    }

    private static int? CalculateFocusScore(FocusCycle cycle)
    {
        if (!cycle.Completed || !cycle.ActualMs.HasValue)
            return null;

        // Simple score based on completion percentage
        var completionRatio = (double)cycle.ActualMs.Value / cycle.PlannedMs;
        var score = (int)Math.Round(completionRatio * 100);
        return Math.Clamp(score, 0, 100);
    }

    private static string GenerateIdempotencyKey(Guid cycleId, DateTime startedAt)
    {
        var input = $"{cycleId}:{startedAt:O}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private async Task NotifyFocusStartAsync()
    {
        var focusMinutes = _policy.GetFocusMinutes();
        var title = _policy.Mode == FocusModeType.Pomodoro
            ? "🍅 Pomodoro iniciado!"
            : "🌊 Ciclo Ultradian iniciado!";

        await _notificationService.SendAsync(
            Contracts.Notifications.AgentNotification.FocusResumeNotification(
                title,
                $"Foco por {focusMinutes} minutos. Bora!",
                new("Ver dashboard", "openDashboard")),
            CancellationToken.None);
    }

    private async Task NotifyFocusEndAsync()
    {
        var breakMinutes = _policy.GetBreakMinutes(_nextBreakType);

        var title = _policy.Mode == FocusModeType.Pomodoro
            ? "⏸ Hora de pausar!"
            : "🌊 Ciclo Ultradian completo!";

        var message = _policy.Mode == FocusModeType.Pomodoro
            ? $"Ciclo {_currentCycleNumber} concluído — {breakMinutes} min de descanso"
            : "Seu cérebro precisa de recuperação. Você merece!";

        await _notificationService.SendAsync(
            Contracts.Notifications.AgentNotification.FocusBreakNotification(
                title,
                message,
                new("Iniciar pausa", "startBreak"),
                new("Pular", "skipBreak")),
            CancellationToken.None);
    }

    private async Task NotifyBreakStartAsync()
    {
        // Apenas log - a notificação de início de pausa é opcional
        _logger.LogInformation("Break started, next break type: {Type}", _nextBreakType);
        await Task.CompletedTask;
    }

    private async Task NotifyBreakEndAsync()
    {
        var focusMinutes = _policy.GetFocusMinutes();

        var title = "✅ Pausa encerrada!";
        var message = _policy.Mode == FocusModeType.Pomodoro
            ? "Próximo bloco de foco começando..."
            : $"Pronto para mais {focusMinutes} min de foco profundo?";

        await _notificationService.SendAsync(
            Contracts.Notifications.AgentNotification.FocusResumeNotification(
                title,
                message,
                new("Voltar ao foco", "startFocus")),
            CancellationToken.None);
    }

    private void OnStateChanged(FocusModeState previous, FocusModeState current, string reason)
    {
        var args = new FocusModeStateChangedEventArgs
        {
            PreviousState = previous,
            CurrentState = current,
            Snapshot = GetSnapshot(),
            Reason = reason,
        };

        StateChanged?.Invoke(this, args);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _cts?.Cancel();
        _cts?.Dispose();
        _timer?.Dispose();

        _disposed = true;
    }
}
