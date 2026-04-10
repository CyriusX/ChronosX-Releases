namespace TimeTrack.Backend.Infrastructure.Jobs.Interfaces;

/// <summary>
/// Hourly job that scans all assigned, not-done tasks whose due date is
/// the current UTC calendar day and drops a one-per-task-per-day
/// DeadlineToday notification into each assignee's inbox.
/// </summary>
public interface IDeadlineScanJob
{
    Task ExecuteAsync();
}
