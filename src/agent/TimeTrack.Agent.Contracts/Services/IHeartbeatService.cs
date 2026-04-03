namespace TimeTrack.Agent.Contracts.Services;

public interface IHeartbeatService
{
    Task<HeartbeatResult> SendHeartbeatAsync(CancellationToken cancellationToken = default);
}

public sealed class HeartbeatResult
{
    public bool Success { get; init; }
    public bool HasPendingCommands { get; init; }
}
