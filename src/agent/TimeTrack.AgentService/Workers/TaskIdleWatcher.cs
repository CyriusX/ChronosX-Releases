using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Providers;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.AgentService.Configuration;
using TimeTrack.AgentService.Ipc;

namespace TimeTrack.AgentService.Workers;

/// <summary>
/// Watches for user idle while a kanban task timer is running.
/// When the user has been idle past the configured threshold AND there is an
/// open TaskTimeEntry, pauses the task on the backend so the idle gap isn't
/// counted toward worked time. On activity resume, emits an IPC event so the
/// desktop shows a "still working on this task?" prompt.
/// </summary>
public sealed class TaskIdleWatcher : BackgroundService
{
    private readonly IIdleDetector _idleDetector;
    private readonly IBackendTasksClient _tasksClient;
    private readonly ICurrentUserContext _userContext;
    private readonly IIpcServer _ipcServer;
    private readonly AgentSettings _settings;
    private readonly ILocalSettingsRepository _localSettingsRepository;
    private readonly IOrgPolicyProvider _orgPolicyProvider;
    private readonly ILogger<TaskIdleWatcher> _logger;

    // Poll cadence matches the ActivityResumeDetector — cheap check, 5s is fine.
    private const int CheckIntervalMs = 5000;

    // Active threshold — idle less than this = user is working.
    private static readonly TimeSpan ActiveThreshold = TimeSpan.FromSeconds(30);

    // Minimum idle time before we pause the task timer (org policy -> local -> default).
    private int _cachedEffectiveIdleThresholdSeconds;
    private DateTime _lastThresholdRefreshUtc = DateTime.MinValue;
    private static readonly TimeSpan ThresholdRefreshInterval = TimeSpan.FromSeconds(60);

    // Cached state of the most recent open task poll so we don't hit the backend on every tick.
    private OpenTaskResult? _lastKnownOpenTask;
    private DateTime _lastTaskPollAt = DateTime.MinValue;
    private static readonly TimeSpan TaskPollInterval = TimeSpan.FromSeconds(15);

    // Whether we've already paused the task due to idle — prevents duplicate calls.
    private bool _pausedByThisWatcher;
    private Guid? _pausedTaskId;

    public TaskIdleWatcher(
        IIdleDetector idleDetector,
        IBackendTasksClient tasksClient,
        ICurrentUserContext userContext,
        IIpcServer ipcServer,
        AgentSettings settings,
        ILocalSettingsRepository localSettingsRepository,
        IOrgPolicyProvider orgPolicyProvider,
        ILogger<TaskIdleWatcher> logger)
    {
        _idleDetector = idleDetector;
        _tasksClient = tasksClient;
        _userContext = userContext;
        _ipcServer = ipcServer;
        _settings = settings;
        _localSettingsRepository = localSettingsRepository;
        _orgPolicyProvider = orgPolicyProvider;
        _logger = logger;
        _cachedEffectiveIdleThresholdSeconds = _settings.IdleThresholdSeconds;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "TaskIdleWatcher started. Idle threshold: {Threshold}s, Check interval: {Interval}ms",
            _settings.IdleThresholdSeconds, CheckIntervalMs);

        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(CheckIntervalMs));

        try
        {
            while (!stoppingToken.IsCancellationRequested &&
                   await timer.WaitForNextTickAsync(stoppingToken))
            {
                await TickAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) { /* expected on shutdown */ }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TaskIdleWatcher crashed");
        }

        _logger.LogInformation("TaskIdleWatcher stopped");
    }

    private async Task TickAsync(CancellationToken ct)
    {
        try
        {
            if (!_userContext.UserId.HasValue) return;

            // Refresh the effective idle threshold periodically (org policy -> local -> default).
            if (DateTime.UtcNow - _lastThresholdRefreshUtc >= ThresholdRefreshInterval)
            {
                _lastThresholdRefreshUtc = DateTime.UtcNow;
                var org = await _orgPolicyProvider.GetIdleThresholdSecondsAsync(ct);
                int? local = null;
                try
                {
                    var s = await _localSettingsRepository.GetAsync(ct);
                    local = s.IdleThresholdSeconds;
                }
                catch
                {
                    // ignore
                }
                _cachedEffectiveIdleThresholdSeconds = org ?? local ?? _settings.IdleThresholdSeconds;
            }

            // Refresh our cached open-task snapshot periodically.
            if (DateTime.UtcNow - _lastTaskPollAt >= TaskPollInterval)
            {
                try
                {
                    _lastKnownOpenTask = await _tasksClient.GetMyOpenTaskAsync(ct);
                    _lastTaskPollAt = DateTime.UtcNow;
                }
                catch
                {
                    // Non-fatal — keep the stale snapshot
                }
            }

            var openTask = _lastKnownOpenTask;
            var idle = await _idleDetector.GetIdleTimeAsync(ct);
            if (!idle.HasValue) return;

            if (openTask is null)
            {
                // No task running — clear any lingering pause flag.
                if (_pausedByThisWatcher)
                {
                    _pausedByThisWatcher = false;
                    _pausedTaskId = null;
                }
                return;
            }

            var idleSec = idle.Value.TotalSeconds;

            // Case 1: user went idle past the threshold while a task is running → pause
            if (!_pausedByThisWatcher && !openTask.IsPaused && idleSec >= _cachedEffectiveIdleThresholdSeconds)
            {
                _logger.LogInformation(
                    "User idle for {IdleSec:F0}s with open task {TaskTitle} — pausing backend timer",
                    idleSec, openTask.TaskTitle);

                var ok = await _tasksClient.PauseMyOpenTaskAsync(ct);
                if (ok)
                {
                    _pausedByThisWatcher = true;
                    _pausedTaskId = openTask.TaskId;
                    // Force a fresh poll next tick so the cached snapshot reflects IsPaused=true.
                    _lastTaskPollAt = DateTime.MinValue;

                    if (_ipcServer.IsClientConnected)
                    {
                        await _ipcServer.SendEventAsync(new IpcEvent
                        {
                            EventType = "taskIdleAutoPaused",
                            Payload = new
                            {
                                taskId = openTask.TaskId,
                                taskTitle = openTask.TaskTitle,
                                projectName = openTask.ProjectName,
                            }
                        }, ct);
                    }
                }
                return;
            }

            // Case 2: we paused earlier, and the user is now active → show the "still working?" prompt.
            if (_pausedByThisWatcher && _pausedTaskId == openTask.TaskId && idle.Value < ActiveThreshold)
            {
                _logger.LogInformation(
                    "User returned while task {TaskTitle} is paused — showing resume prompt",
                    openTask.TaskTitle);

                // Clear the flag so we don't re-send the prompt on every tick.
                _pausedByThisWatcher = false;

                if (_ipcServer.IsClientConnected)
                {
                    await _ipcServer.SendEventAsync(new IpcEvent
                    {
                        EventType = "showTaskResumePrompt",
                        Payload = new
                        {
                            taskId = openTask.TaskId,
                            taskTitle = openTask.TaskTitle,
                            projectName = openTask.ProjectName,
                            projectColor = openTask.ProjectColor,
                            countdownSeconds = 30,
                        }
                    }, ct);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TaskIdleWatcher tick failed");
        }
    }
}
