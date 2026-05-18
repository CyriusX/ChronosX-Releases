using System.Collections.Concurrent;
using System.Threading.RateLimiting;

namespace TimeTrack.Api.OpsMcp;

public sealed class McpPerKeyRateLimiter
{
    private readonly ConcurrentDictionary<Guid, FixedWindowRateLimiter> _limiters = new();
    private readonly int _permitLimit;
    private readonly TimeSpan _window;

    public McpPerKeyRateLimiter(IConfiguration configuration)
    {
        _permitLimit = configuration.GetValue<int?>("Mcp:RateLimit:PermitLimit") ?? 300;
        var windowSeconds = configuration.GetValue<int?>("Mcp:RateLimit:WindowSeconds") ?? 60;
        _window = TimeSpan.FromSeconds(windowSeconds);
    }

    public async ValueTask<(bool Allowed, TimeSpan? RetryAfter)> TryAcquireAsync(Guid platformApiKeyId, CancellationToken cancellationToken)
    {
        var limiter = _limiters.GetOrAdd(platformApiKeyId, _ =>
            new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
            {
                PermitLimit = _permitLimit,
                Window = _window,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                AutoReplenishment = true
            }));

        var lease = await limiter.AcquireAsync(1, cancellationToken);
        if (lease.IsAcquired) return (true, null);

        if (lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            return (false, retryAfter);
        }

        return (false, _window);
    }
}

