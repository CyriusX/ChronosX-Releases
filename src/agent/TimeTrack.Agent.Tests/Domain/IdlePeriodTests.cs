using FluentAssertions;
using Xunit;
using TimeTrack.Agent.Domain.Common;
using TimeTrack.Agent.Domain.Entities;
using TimeTrack.Agent.Domain.ValueObjects;

namespace TimeTrack.Agent.Tests.Domain;

public class IdlePeriodTests
{
    private readonly Guid _testUserId = Guid.NewGuid();
    private readonly TimeRange _testPeriod = new(
        DateTime.UtcNow,
        DateTime.UtcNow.AddMinutes(5));

    [Fact]
    public void Create_WithValidData_ShouldCreateIdlePeriod()
    {
        // Act
        var idle = IdlePeriod.Create(_testUserId, _testPeriod, thresholdSeconds: 60);

        // Assert
        idle.Id.Should().NotBe(Guid.Empty);
        idle.UserId.Should().Be(_testUserId);
        idle.Period.Should().Be(_testPeriod);
        idle.ThresholdSeconds.Should().Be(60);
        idle.IsSystemDetected.Should().BeTrue();
        idle.Duration.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void Create_WithManualDetection_ShouldSetIsSystemDetectedFalse()
    {
        // Act
        var idle = IdlePeriod.Create(_testUserId, _testPeriod, thresholdSeconds: 60, isSystemDetected: false);

        // Assert
        idle.IsSystemDetected.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithNullPeriod_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new IdlePeriod(Guid.NewGuid(), _testUserId, null!, thresholdSeconds: 60);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithZeroThreshold_ShouldThrowDomainException()
    {
        // Act
        var act = () => IdlePeriod.Create(_testUserId, _testPeriod, thresholdSeconds: 0);

        // Assert
        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "INVALID_THRESHOLD");
    }

    [Fact]
    public void Constructor_WithNegativeThreshold_ShouldThrowDomainException()
    {
        // Act
        var act = () => IdlePeriod.Create(_testUserId, _testPeriod, thresholdSeconds: -10);

        // Assert
        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "INVALID_THRESHOLD");
    }

    [Fact]
    public void ExceedsThreshold_WhenExceeded_ShouldReturnTrue()
    {
        // Arrange
        var idle = IdlePeriod.Create(_testUserId, _testPeriod, thresholdSeconds: 60);

        // Act & Assert (5 minutes = 300 seconds > 60 seconds)
        idle.ExceedsThreshold(60).Should().BeTrue();
    }

    [Fact]
    public void ExceedsThreshold_WhenNotExceeded_ShouldReturnFalse()
    {
        // Arrange
        var idle = IdlePeriod.Create(_testUserId, _testPeriod, thresholdSeconds: 60);

        // Act & Assert (5 minutes = 300 seconds < 600 seconds)
        idle.ExceedsThreshold(600).Should().BeFalse();
    }

    [Fact]
    public void Duration_ShouldReturnPeriodDuration()
    {
        // Arrange
        var start = DateTime.UtcNow;
        var end = start.AddMinutes(10);
        var period = new TimeRange(start, end);
        var idle = IdlePeriod.Create(_testUserId, period, thresholdSeconds: 60);

        // Assert
        idle.Duration.Should().Be(TimeSpan.FromMinutes(10));
    }
}
