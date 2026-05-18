namespace TimeTrack.Backend.Application.Common.Interfaces;

/// <summary>
/// Best-effort notifier used to trigger MCP clients subscribed to pushable resources.
/// Implementations must never throw.
/// </summary>
public interface IOpsMcpPushNotifier
{
    Task NotifyResourceUpdatedAsync(string uri, CancellationToken cancellationToken = default);
}

