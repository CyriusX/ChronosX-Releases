using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Microsoft.Extensions.DependencyInjection;

namespace TimeTrack.Api.OpsMcp;

public static class OpsMcpSubscriptions
{
    public static readonly HashSet<string> PushableUris = new(StringComparer.Ordinal)
    {
        "ops://critical",
        "ops://platform-health",
    };

    public static ValueTask<EmptyResult> SubscribeAsync(
        RequestContext<SubscribeRequestParams> request,
        CancellationToken cancellationToken)
    {
        var registry = request.Services.GetRequiredService<OpsMcpSubscriptionRegistry>();
        var uri = request.Params.Uri;
        if (string.IsNullOrWhiteSpace(uri) || !PushableUris.Contains(uri))
        {
            throw new InvalidOperationException("Resource is not subscribable.");
        }

        if (!registry.TryRegisterSession(request.Server))
        {
            throw new InvalidOperationException("Session is not initialized yet.");
        }

        var sessionId = request.Server.SessionId!;
        registry.Subscribe(sessionId, uri);

        return ValueTask.FromResult(new EmptyResult());
    }

    public static ValueTask<EmptyResult> UnsubscribeAsync(
        RequestContext<UnsubscribeRequestParams> request,
        CancellationToken cancellationToken)
    {
        var registry = request.Services.GetRequiredService<OpsMcpSubscriptionRegistry>();
        var uri = request.Params.Uri;
        if (string.IsNullOrWhiteSpace(uri))
        {
            return ValueTask.FromResult(new EmptyResult());
        }

        var sessionId = request.Server.SessionId;
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            registry.Unsubscribe(sessionId!, uri);
        }

        return ValueTask.FromResult(new EmptyResult());
    }
}
