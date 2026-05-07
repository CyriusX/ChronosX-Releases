namespace TimeTrack.Backend.Infrastructure.Jobs.Interfaces;

/// <summary>
/// Background watchdog that pauses open task timers when a user's devices have stopped heartbeating.
/// Prevents open TaskTimeEntry rows from being treated as "running until now" in reporting views.
/// </summary>
public interface ITaskTimerStalePauseJob
{
    Task ExecuteAsync();
}

