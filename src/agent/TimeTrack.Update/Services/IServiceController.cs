namespace TimeTrack.Update.Services;

/// <summary>
/// Controls Windows services
/// </summary>
public interface IServiceController
{
    /// <summary>
    /// Stop a Windows service
    /// </summary>
    Task<bool> StopServiceAsync(string serviceName, TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Start a Windows service
    /// </summary>
    Task<bool> StartServiceAsync(string serviceName, TimeSpan timeout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the current status of a service
    /// </summary>
    ServiceStatus GetServiceStatus(string serviceName);

    /// <summary>
    /// Wait for a service to reach a specific status
    /// </summary>
    Task<bool> WaitForStatusAsync(string serviceName, ServiceStatus desiredStatus, TimeSpan timeout, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service status
/// </summary>
public enum ServiceStatus
{
    Stopped,
    StartPending,
    StopPending,
    Running,
    ContinuePending,
    PausePending,
    Paused
}
