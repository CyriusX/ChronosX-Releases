namespace TimeTrack.Backend.Infrastructure.Jobs.Interfaces;

public interface IWeeklyFeatureAggregationJob
{
    Task ExecuteAsync();
}

public interface IMonthlyFeatureAggregationJob
{
    Task ExecuteAsync();
}
