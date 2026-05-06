namespace TimeTrack.Backend.Infrastructure.Jobs.Interfaces;

public interface ISubscriptionStatusJob
{
    Task ExecuteAsync();
}

public interface IUsageCounterRefreshJob
{
    Task ExecuteAsync();
}
