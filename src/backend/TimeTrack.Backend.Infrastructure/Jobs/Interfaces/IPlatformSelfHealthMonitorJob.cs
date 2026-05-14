namespace TimeTrack.Backend.Infrastructure.Jobs.Interfaces;

public interface IPlatformSelfHealthMonitorJob
{
    Task ExecuteAsync();
}

