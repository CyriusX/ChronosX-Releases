using FluentAssertions;
using Xunit;
using TimeTrack.Agent.Domain.Common;
using TimeTrack.Agent.Domain.Entities;
using TimeTrack.Agent.Domain.ValueObjects;

namespace TimeTrack.Agent.Tests.Domain;

public class ActivitySessionTests
{
    private readonly Guid _testUserId = Guid.NewGuid();
    private readonly AppIdentity _testApp = new("hash123", "Test App");
    private readonly TimeRange _testPeriod = new(
        DateTime.UtcNow,
        DateTime.UtcNow.AddHours(1));

    [Fact]
    public void Create_WithValidData_ShouldCreateSession()
    {
        // Act
        var session = ActivitySession.Create(_testUserId, _testApp, _testPeriod);

        // Assert
        session.Id.Should().NotBe(Guid.Empty);
        session.UserId.Should().Be(_testUserId);
        session.App.Should().Be(_testApp);
        session.Period.Should().Be(_testPeriod);
        session.Duration.Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public void Create_WithWindowInfo_ShouldSetWindowFields()
    {
        // Arrange
        const string windowHash = "window123";
        const string windowTitle = "Document - VS Code";

        // Act
        var session = ActivitySession.Create(_testUserId, _testApp, _testPeriod, windowHash, windowTitle);

        // Assert
        session.WindowHash.Should().Be(windowHash);
        session.WindowTitle.Should().Be(windowTitle);
    }

    [Fact]
    public void Constructor_WithNullApp_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new ActivitySession(Guid.NewGuid(), _testUserId, null!, _testPeriod);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullPeriod_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new ActivitySession(Guid.NewGuid(), _testUserId, _testApp, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Extend_ShouldExtendPeriod()
    {
        // Arrange
        var start = DateTime.UtcNow;
        var initialEnd = start.AddMinutes(30);
        var newEnd = start.AddHours(1);
        var period = new TimeRange(start, initialEnd);
        var session = ActivitySession.Create(_testUserId, _testApp, period);

        // Act
        session.Extend(newEnd);

        // Assert
        session.Period.EndUtc.Should().Be(newEnd);
        session.Duration.Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public void Extend_WithTimeBeforeStart_ShouldThrowDomainException()
    {
        // Arrange
        var start = DateTime.UtcNow;
        var end = start.AddHours(1);
        var period = new TimeRange(start, end);
        var session = ActivitySession.Create(_testUserId, _testApp, period);

        // Act
        var act = () => session.Extend(start.AddMinutes(-10));

        // Assert
        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "INVALID_TIME_RANGE");
    }

    [Fact]
    public void Duration_ShouldReturnPeriodDuration()
    {
        // Arrange
        var start = DateTime.UtcNow;
        var end = start.AddMinutes(45);
        var period = new TimeRange(start, end);
        var session = ActivitySession.Create(_testUserId, _testApp, period);

        // Assert
        session.Duration.Should().Be(TimeSpan.FromMinutes(45));
    }
}
