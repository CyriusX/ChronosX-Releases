using ModelContextProtocol.Protocol;
using TimeTrack.Backend.Application.Common.Interfaces;

namespace TimeTrack.Api.OpsMcp;

public sealed class OpsMcpPushNotifier : IOpsMcpPushNotifier
{
    private readonly OpsMcpSubscriptionRegistry _registry;
    private readonly ILogger<OpsMcpPushNotifier> _logger;

    public OpsMcpPushNotifier(
        OpsMcpSubscriptionRegistry registry,
        ILogger<OpsMcpPushNotifier> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    public async Task NotifyResourceUpdatedAsync(string uri, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(uri)) return;

            var sessions = _registry.GetSubscribedSessions(uri);
            if (sessions.Count == 0) return;

            foreach (var session in sessions)
            {
                var sessionId = session.SessionId;
                if (string.IsNullOrWhiteSpace(sessionId)) continue;

                try
                {
                    await session.SendNotificationAsync(
                        NotificationMethods.ResourceUpdatedNotification,
                        new ResourceUpdatedNotificationParams { Uri = uri },
                        cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed sending MCP resource updated notification. Removing session {SessionId}", sessionId);
                    _registry.RemoveSession(sessionId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unhandled error while notifying MCP resource update for {Uri}", uri);
        }
    }
}

