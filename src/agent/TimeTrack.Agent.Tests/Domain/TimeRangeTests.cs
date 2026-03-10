using FluentAssertions;
using Xunit;
using TimeTrack.Agent.Domain.Common;
using TimeTrack.Agent.Domain.ValueObjects;

namespace TimeTrack.Agent.Tests.Domain;

public class TimeRangeTests
{
    [Fact]
    public void Constructor_WithValidRange_ShouldCreateTimeRange()
    {
        // Arrange
        var start = DateTime.UtcNow;
        var end = start.AddHours(1);

        // Act
        var timeRange = new TimeRange(start, end);

        // Assert
        timeRange.StartUtc.Should().Be(start);
        timeRange.EndUtc.Should().Be(end);
        timeRange.Duration.Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public void Constructor_WithEndBeforeStart_ShouldThrowDomainException()
    {
        // Arrange
        var start = DateTime.UtcNow;
        var end = start.AddHours(-1);

        // Act
        var act = () => new TimeRange(start, end);

        // Assert
        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "INVALID_TIME_RANGE");
    }

    [Fact]
    public void Constructor_WithEqualStartAndEnd_ShouldThrowDomainException()
    {
        // Arrange
        var moment = DateTime.UtcNow;

        // Act
        var act = () => new TimeRange(moment, moment);

        // Assert
        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "INVALID_TIME_RANGE");
    }

    [Fact]
    public void FromDuration_ShouldCreateTimeRangeFromNow()
    {
        // Arrange
        var duration = TimeSpan.FromMinutes(30);

        // Act
        var timeRange = TimeRange.FromDuration(duration);

        // Assert
        timeRange.Duration.Should().Be(duration);
        timeRange.EndUtc.Should().Be(timeRange.StartUtc.Add(duration));
    }

    [Fact]
    public void FromStartAndDuration_ShouldCreateTimeRange()
    {
        // Arrange
        var start = DateTime.UtcNow;
        var duration = TimeSpan.FromHours(2);

        // Act
        var timeRange = TimeRange.FromStartAndDuration(start, duration);

        // Assert
        timeRange.StartUtc.Should().Be(start);
        timeRange.Duration.Should().Be(duration);
    }

    [Fact]
    public void Overlaps_WithOverlappingRanges_ShouldReturnTrue()
    {
        // Arrange
        var range1 = new TimeRange(
            new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc));

        var range2 = new TimeRange(
            new DateTime(2024, 1, 1, 11, 0, 0, DateTimeKind.Utc),
            new DateTime(2024, 1, 1, 13, 0, 0, DateTimeKind.Utc));

        // Act & Assert
        range1.Overlaps(range2).Should().BeTrue();
        range2.Overlaps(range1).Should().BeTrue();
    }

    [Fact]
    public void Overlaps_WithNonOverlappingRanges_ShouldReturnFalse()
    {
        // Arrange
        var range1 = new TimeRange(
            new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc));

        var range2 = new TimeRange(
            new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2024, 1, 1, 14, 0, 0, DateTimeKind.Utc));

        // Act & Assert
        range1.Overlaps(range2).Should().BeFalse();
    }

    [Fact]
    public void Contains_WithMomentInsideRange_ShouldReturnTrue()
    {
        // Arrange
        var range = new TimeRange(
            new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc));

        var moment = new DateTime(2024, 1, 1, 11, 0, 0, DateTimeKind.Utc);

        // Act & Assert
        range.Contains(moment).Should().BeTrue();
    }

    [Fact]
    public void Contains_WithMomentOutsideRange_ShouldReturnFalse()
    {
        // Arrange
        var range = new TimeRange(
            new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc));

        var moment = new DateTime(2024, 1, 1, 13, 0, 0, DateTimeKind.Utc);

        // Act & Assert
        range.Contains(moment).Should().BeFalse();
    }

    [Fact]
    public void Equality_WithSameValues_ShouldBeEqual()
    {
        // Arrange
        var start = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var range1 = new TimeRange(start, end);
        var range2 = new TimeRange(start, end);

        // Assert
        range1.Should().Be(range2);
        range1.GetHashCode().Should().Be(range2.GetHashCode());
    }
}
