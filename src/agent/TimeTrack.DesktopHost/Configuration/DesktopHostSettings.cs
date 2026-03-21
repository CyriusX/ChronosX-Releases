namespace TimeTrack.DesktopHost.Configuration;

/// <summary>
/// Configuration settings for DesktopHost
/// </summary>
public sealed class DesktopHostSettings
{
    /// <summary>
    /// Path to the React UI bundle (relative to executable)
    /// </summary>
    public string UiBundlePath { get; set; } = "ui";

    /// <summary>
    /// Named Pipe name for IPC communication with AgentService
    /// </summary>
    public string PipeName { get; set; } = "TimeTrack.Agent.IPC";

    /// <summary>
    /// Connection timeout in milliseconds
    /// </summary>
    public int ConnectionTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Reconnection delay in milliseconds
    /// </summary>
    public int ReconnectionDelayMs { get; set; } = 1000;

    /// <summary>
    /// Maximum reconnection attempts
    /// </summary>
    public int MaxReconnectionAttempts { get; set; } = 10;

    /// <summary>
    /// Window width in pixels
    /// </summary>
    public int WindowWidth { get; set; } = 1280;

    /// <summary>
    /// Window height in pixels
    /// </summary>
    public int WindowHeight { get; set; } = 800;

    /// <summary>
    /// Application title
    /// </summary>
    public string AppTitle { get; set; } = "XChronus";

    /// <summary>
    /// Whether to start minimized to tray
    /// </summary>
    public bool StartMinimized { get; set; } = false;

    /// <summary>
    /// Gets the max reconnection attempts (alias for MaxReconnectionAttempts)
    /// </summary>
    public int MaxReconnectionAttemptsValue => MaxReconnectionAttempts;
}
