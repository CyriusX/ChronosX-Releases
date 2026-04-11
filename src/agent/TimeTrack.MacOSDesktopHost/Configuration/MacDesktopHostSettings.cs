namespace TimeTrack.MacOSDesktopHost.Configuration;

public sealed class MacDesktopHostSettings
{
    public string SocketPath { get; set; } = "/var/tmp/TimeTrack.Agent.IPC";

    public string UiBundlePath { get; set; } = "ui";

    public int ConnectionTimeoutMs { get; set; } = 5000;

    public int ReconnectionDelayMs { get; set; } = 1000;

    public int MaxReconnectionAttempts { get; set; } = 10;

    public int WindowWidth { get; set; } = 1280;

    public int WindowHeight { get; set; } = 800;

    public string AppTitle { get; set; } = "ChronosX";

    public bool StartMinimized { get; set; } = false;
}
