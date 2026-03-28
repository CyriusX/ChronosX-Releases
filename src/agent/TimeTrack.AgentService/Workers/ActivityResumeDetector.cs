using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Providers;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Domain.Enums;
using TimeTrack.AgentService.Configuration;
using TimeTrack.AgentService.Ipc;

namespace TimeTrack.AgentService.Workers;

/// <summary>
/// Background service that detects continuous user activity while tracking is paused.
/// When the user has been active for a configured duration, sends an IPC event
/// to the DesktopHost to show a resume prompt toast.
/// </summary>
public sealed class ActivityResumeDetector : BackgroundService
{
    private readonly IIdleDetector _idleDetector;
    private readonly ITrackingStateRepository _stateRepository;
    private readonly ICurrentUserContext _userContext;
    private readonly IIpcServer _ipcServer;
    private readonly ActivityResumeState _resumeState;
    private readonly AgentSettings _settings;
    private readonly ILogger<ActivityResumeDetector> _logger;

    /// <summary>
    /// Continuous activity must last this long before showing the prompt.
    /// Default: 60s for testing (production: 300s).
    /// </summary>
    private int ActivityResumeThresholdSeconds => _settings.ActivityResumeThresholdSeconds;

    /// <summary>
    /// How often to check for activity (ms). Reuses polling cadence.
    /// </summary>
    private const int CheckIntervalMs = 5000;

    /// <summary>
    /// If idle time is below this threshold, the user is considered "active".
    /// </summary>
    private static readonly TimeSpan ActiveThreshold = TimeSpan.FromSeconds(30);

    private DateTime? _continuousActivityStart;

    public ActivityResumeDetector(
        IIdleDetector idleDetector,
        ITrackingStateRepository stateRepository,
        ICurrentUserContext userContext,
        IIpcServer ipcServer,
        ActivityResumeState resumeState,
        AgentSettings settings,
        ILogger<ActivityResumeDetector> logger)
    {
        _idleDetector = idleDetector;
        _stateRepository = stateRepository;
        _userContext = userContext;
        _ipcServer = ipcServer;
        _resumeState = resumeState;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "ActivityResumeDetector started. Threshold: {Threshold}s, Check interval: {Interval}ms",
            ActivityResumeThresholdSeconds, CheckIntervalMs);

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(CheckIntervalMs));

        try
        {
            while (!stoppingToken.IsCancellationRequested &&
                   await timer.WaitForNextTickAsync(stoppingToken))
            {
                await CheckActivityAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ActivityResumeDetector crashed");
        }

        _logger.LogInformation("ActivityResumeDetector stopped");
    }

    private async Task CheckActivityAsync(CancellationToken ct)
    {
        try
        {
            // Must have an authenticated user
            var userId = _userContext.UserId;
            if (!userId.HasValue) return;

            // Get tracking state
            var state = await _stateRepository.GetAsync(userId.Value, ct);
            if (state == null)
            {
                ResetDetection();
                return;
            }

            // Only trigger for manual pauses (PausedByUser)
            if (state.Status != TrackingStatus.PausedByUser)
            {
                if (_continuousActivityStart.HasValue)
                {
                    _logger.LogDebug("Tracking no longer paused by user — resetting activity detection");
                    ResetDetection();
                    _resumeState.Reset();
                }
                return;
            }

            // Skip if user already dismissed the prompt for this pause
            if (_resumeState.IsDismissedForCurrentPause) return;

            // Skip if prompt is currently showing
            if (_resumeState.IsPromptCurrentlyShowing) return;

            // Check idle time
            var idleTime = await _idleDetector.GetIdleTimeAsync(ct);
            if (!idleTime.HasValue) return;

            if (idleTime.Value < ActiveThreshold)
            {
                // User is active
                if (!_continuousActivityStart.HasValue)
                {
                    _continuousActivityStart = DateTime.UtcNow;
                    _logger.LogDebug("User activity detected while paused — starting timer");
                }

                var activeFor = DateTime.UtcNow - _continuousActivityStart.Value;
                if (activeFor.TotalSeconds >= ActivityResumeThresholdSeconds)
                {
                    _logger.LogInformation(
                        "User has been active for {Seconds:F0}s while paused — sending resume prompt",
                        activeFor.TotalSeconds);

                    _resumeState.SetPromptShowing(true);
                    _continuousActivityStart = null; // Reset to prevent re-sending

                    await _ipcServer.SendEventAsync(new IpcEvent
                    {
                        EventType = "showActivityResumePrompt",
                        Payload = new { countdownSeconds = 20 }
                    }, ct);
                }
            }
            else
            {
                // User went idle — reset the continuous activity timer
                if (_continuousActivityStart.HasValue)
                {
                    _logger.LogDebug("User went idle — resetting activity timer");
                    _continuousActivityStart = null;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in activity resume detection cycle");
        }
    }

    private void ResetDetection()
    {
        _continuousActivityStart = null;
    }
}
