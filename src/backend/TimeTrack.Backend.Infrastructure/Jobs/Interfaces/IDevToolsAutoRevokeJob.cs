namespace TimeTrack.Backend.Infrastructure.Jobs.Interfaces;

/// <summary>
/// Background watchdog that auto-revokes expired DevTools access flags and pushes
/// disable commands to affected devices.
/// </summary>
public interface IDevToolsAutoRevokeJob
{
    Task ExecuteAsync();
}

