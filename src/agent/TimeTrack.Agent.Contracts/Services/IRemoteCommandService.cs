namespace TimeTrack.Agent.Contracts.Services;

public interface IRemoteCommandService
{
    Task PollAndExecuteAsync(CancellationToken cancellationToken = default);
}
