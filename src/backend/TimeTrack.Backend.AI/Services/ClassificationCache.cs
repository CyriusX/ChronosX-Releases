using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TimeTrack.Backend.AI.Configuration;
using TimeTrack.Backend.AI.Interfaces;

namespace TimeTrack.Backend.AI.Services;

public sealed class ClassificationCache
{
    private readonly IMemoryCache _cache;
    private readonly ZAiOptions _options;
    private readonly ILogger<ClassificationCache> _logger;

    public ClassificationCache(
        IMemoryCache cache,
        IOptions<ZAiOptions> options,
        ILogger<ClassificationCache> logger)
    {
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public ClassificationResult? Get(string exeName)
    {
        var cacheKey = BuildKey(exeName);
        return _cache.TryGetValue<ClassificationResult>(cacheKey, out var cached) ? cached : null;
    }

    public void Set(string exeName, ClassificationResult result)
    {
        if (result.Confidence <= 0) return;

        var cacheKey = BuildKey(exeName);
        var cacheDuration = result.Confidence >= 0.85
            ? TimeSpan.FromHours(_options.HighConfidenceCacheHours)
            : TimeSpan.FromHours(Math.Max(1, _options.CacheDurationHours / 24));

        _cache.Set(cacheKey, result, cacheDuration);
    }

    public void Invalidate(string exeName)
    {
        var cacheKey = BuildKey(exeName);
        _cache.Remove(cacheKey);
        _logger.LogInformation("Invalidated classification cache for {ExeName}", exeName);
    }

    private static string BuildKey(string exeName) =>
        $"app-classification:{exeName.ToLowerInvariant()}";
}
