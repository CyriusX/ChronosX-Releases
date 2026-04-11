using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TimeTrack.Agent.Infrastructure.MacOS.Providers;
using Xunit;

namespace TimeTrack.Agent.Tests.MacOS.Providers;

public class MacOSIdleDetectorTests
{
    private readonly Mock<ILogger<MacOSIdleDetector>> _loggerMock;

    public MacOSIdleDetectorTests()
    {
        _loggerMock = new Mock<ILogger<MacOSIdleDetector>>();
    }

    [Fact]
    public async Task GetIdleTimeAsync_Should_ReturnTimeSpan_When_ApiSucceeds()
    {
        var detector = new MacOSIdleDetector(_loggerMock.Object);

        var result = await detector.GetIdleTimeAsync();

        result.Should().NotBeNull();
        result!.Value.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public async Task IsIdleAsync_Should_ReturnTrue_When_IdleExceedsThreshold()
    {
        var detector = new MacOSIdleDetector(_loggerMock.Object);

        var result = await detector.IsIdleAsync(TimeSpan.Zero);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsIdleAsync_Should_ReturnFalse_When_IdleLessThanThreshold()
    {
        var detector = new MacOSIdleDetector(_loggerMock.Object);

        var result = await detector.IsIdleAsync(TimeSpan.FromDays(365));

        result.Should().BeFalse();
    }
}
