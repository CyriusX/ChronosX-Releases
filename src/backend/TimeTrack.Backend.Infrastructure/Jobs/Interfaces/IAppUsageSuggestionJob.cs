namespace TimeTrack.Backend.Infrastructure.Jobs.Interfaces;

public interface IAppUsageSuggestionJob
{
    Task ExecuteAsync();
    Task ExecuteForOrgAsync(Guid orgId);
}
