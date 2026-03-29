namespace TimeTrack.Backend.Domain.Constants;

/// <summary>
/// Internal/system app process names that should be excluded from all
/// dashboard totals, reports, and score calculations.
///
/// These are our own tracking UI processes and OS-level entries that
/// inflate work time if counted.
///
/// IMPORTANT: Any change here affects all dashboard views, reports,
/// team status, member summaries, and focus score calculations.
/// Keep in sync with the agent's local filter in GetLocalDashboardUseCase.
/// </summary>
public static class InternalApps
{
    public static readonly HashSet<string> ProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "TimeTrack.DesktopHost",
        "Microsoft Edge WebView2",
        "Microsoft® Windows® Operating System",
        "Sistema operacional Microsoft® Windows®",
        "Tracking Stopped",
    };

    /// <summary>
    /// Returns true if the process name is an internal/system app that should be excluded.
    /// </summary>
    public static bool IsInternal(string? processName)
        => !string.IsNullOrEmpty(processName) && ProcessNames.Contains(processName);
}
