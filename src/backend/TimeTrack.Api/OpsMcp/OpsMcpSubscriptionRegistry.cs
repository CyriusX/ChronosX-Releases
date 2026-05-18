using System.Collections.Concurrent;
using ModelContextProtocol.Server;

namespace TimeTrack.Api.OpsMcp;

public sealed class OpsMcpSubscriptionRegistry
{
    private readonly ConcurrentDictionary<string, McpServer> _sessions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _subscriptionsByUri = new(StringComparer.Ordinal);

    public bool TryRegisterSession(McpServer server)
    {
        var sessionId = server.SessionId;
        if (string.IsNullOrWhiteSpace(sessionId))
            return false;

        _sessions[sessionId] = server;
        return true;
    }

    public void RemoveSession(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return;

        _sessions.TryRemove(sessionId, out _);

        foreach (var kvp in _subscriptionsByUri)
        {
            kvp.Value.TryRemove(sessionId, out _);
        }
    }

    public void Subscribe(string sessionId, string uri)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(uri)) return;

        var set = _subscriptionsByUri.GetOrAdd(uri, _ => new ConcurrentDictionary<string, byte>(StringComparer.Ordinal));
        set[sessionId] = 1;
    }

    public void Unsubscribe(string sessionId, string uri)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(uri)) return;

        if (_subscriptionsByUri.TryGetValue(uri, out var set))
        {
            set.TryRemove(sessionId, out _);
        }
    }

    public IReadOnlyList<McpServer> GetSubscribedSessions(string uri)
    {
        if (!_subscriptionsByUri.TryGetValue(uri, out var set))
            return Array.Empty<McpServer>();

        var result = new List<McpServer>();
        foreach (var sessionId in set.Keys)
        {
            if (_sessions.TryGetValue(sessionId, out var server))
            {
                result.Add(server);
            }
        }

        return result;
    }
}

