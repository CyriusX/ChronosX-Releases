using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TimeTrack.Backend.AI.Configuration;
using TimeTrack.Backend.AI.Interfaces;
using TimeTrack.Backend.AI.Services;
using Xunit;

namespace TimeTrack.Backend.Tests.AI;

public sealed class ClassificationCacheTests : IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly IOptions<ZAiOptions> _options;
    private readonly Mock<ILogger<ClassificationCache>> _loggerMock;
    private readonly ClassificationCache _sut;

    public ClassificationCacheTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _options = Options.Create(new ZAiOptions
        {
            CacheDurationHours = 24,
            HighConfidenceCacheHours = 168,
        });
        _loggerMock = new Mock<ILogger<ClassificationCache>>();
        _sut = new ClassificationCache(_cache, _options, _loggerMock.Object);
    }

    public void Dispose() => _cache.Dispose();

    [Fact]
    public void Get_WhenNotCached_ReturnsNull()
    {
        _sut.Get("chrome").Should().BeNull();
    }

    [Fact]
    public void Set_ThenGet_ReturnsCachedResult()
    {
        var result = new ClassificationResult
        {
            Category = "productive",
            Subcategory = "ide",
            Confidence = 0.9,
        };

        _sut.Set("code", result);
        var cached = _sut.Get("code");

        cached.Should().NotBeNull();
        cached!.Category.Should().Be("productive");
        cached.Confidence.Should().BeApproximately(0.9, 0.001);
    }

    [Fact]
    public void Set_IsCaseInsensitive()
    {
        var result = new ClassificationResult
        {
            Category = "distracting",
            Subcategory = "social_media",
            Confidence = 0.8,
        };

        _sut.Set("Discord", result);
        var cached = _sut.Get("discord");

        cached.Should().NotBeNull();
        cached!.Category.Should().Be("distracting");
    }

    [Fact]
    public void Set_WithZeroConfidence_DoesNotCache()
    {
        var result = new ClassificationResult
        {
            Category = "neutral",
            Subcategory = "unclassified",
            Confidence = 0,
        };

        _sut.Set("test-app", result);
        _sut.Get("test-app").Should().BeNull();
    }

    [Fact]
    public void Invalidate_RemovesCachedEntry()
    {
        var result = new ClassificationResult
        {
            Category = "productive",
            Subcategory = "communication",
            Confidence = 0.75,
        };

        _sut.Set("slack", result);
        _sut.Get("slack").Should().NotBeNull();

        _sut.Invalidate("slack");
        _sut.Get("slack").Should().BeNull();
    }

    [Fact]
    public void Invalidate_IsCaseInsensitive()
    {
        var result = new ClassificationResult
        {
            Category = "productive",
            Subcategory = "browser",
            Confidence = 0.85,
        };

        _sut.Set("Chrome", result);
        _sut.Invalidate("chrome");
        _sut.Get("CHROME").Should().BeNull();
    }
}
