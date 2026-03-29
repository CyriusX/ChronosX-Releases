namespace TimeTrack.AgentService.Workers;

/// <summary>
/// Thread-safe shared state for the activity resume detection feature.
/// Tracks whether the resume prompt has been dismissed for the current pause session.
/// </summary>
public sealed class ActivityResumeState
{
    private volatile bool _dismissedForCurrentPause;
    private volatile bool _promptCurrentlyShowing;

    /// <summary>
    /// Whether the user clicked "No" on the resume prompt for this pause session.
    /// </summary>
    public bool IsDismissedForCurrentPause => _dismissedForCurrentPause;

    /// <summary>
    /// Whether a resume prompt is currently being displayed.
    /// </summary>
    public bool IsPromptCurrentlyShowing => _promptCurrentlyShowing;

    /// <summary>
    /// Mark the prompt as dismissed — don't ask again until next pause.
    /// </summary>
    public void Dismiss()
    {
        _dismissedForCurrentPause = true;
        _promptCurrentlyShowing = false;
    }

    /// <summary>
    /// Set whether a prompt is currently showing.
    /// </summary>
    public void SetPromptShowing(bool showing) => _promptCurrentlyShowing = showing;

    /// <summary>
    /// Reset all state — called when tracking transitions out of paused state.
    /// </summary>
    public void Reset()
    {
        _dismissedForCurrentPause = false;
        _promptCurrentlyShowing = false;
    }
}
