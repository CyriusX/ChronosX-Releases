using FluentAssertions;
using Xunit;
using TimeTrack.Agent.Domain.Aggregates;
using TimeTrack.Agent.Domain.Common;
using TimeTrack.Agent.Domain.Enums;
using TimeTrack.Agent.Domain.Events;

namespace TimeTrack.Agent.Tests.Domain;

public class TrackingStateTests
{
    #region Creation

    [Fact]
    public void CreateActive_ShouldCreateWithActiveStatus()
    {
        // Act
        var state = TrackingState.CreateActive();

        // Assert
        state.Status.Should().Be(TrackingStatus.Active);
        state.IsActive.Should().BeTrue();
        state.IsPaused.Should().BeFalse();
        state.IsDisabled.Should().BeFalse();
        state.Id.Should().NotBe(Guid.Empty);
    }

    #endregion

    #region Pause - Valid Transitions

    [Fact]
    public void Pause_FromActive_ShouldSucceed()
    {
        // Arrange
        var state = TrackingState.CreateActive();

        // Act
        state.Pause("Taking a break", "user123");

        // Assert
        state.Status.Should().Be(TrackingStatus.PausedByUser);
        state.IsPaused.Should().BeTrue();
        state.Reason.Should().Be("Taking a break");
        state.PausedAt.Should().NotBeNull();
        state.LastModifiedBy.Should().Be("user123");
    }

    [Fact]
    public void Pause_WithIsPolicy_ShouldSetPausedByPolicy()
    {
        // Arrange
        var state = TrackingState.CreateActive();

        // Act
        state.Pause("Inactivity timeout", "system", isPolicy: true);

        // Assert
        state.Status.Should().Be(TrackingStatus.PausedByPolicy);
    }

    [Fact]
    public void Pause_ShouldRaiseTrackingPausedEvent()
    {
        // Arrange
        var state = TrackingState.CreateActive();

        // Act
        state.Pause("Break", "user123");

        // Assert
        state.Events.Should().HaveCount(1);
        var @event = state.Events[0].Should().BeOfType<TrackingPaused>().Subject;
        @event.Reason.Should().Be("Break");
        @event.PausedBy.Should().Be("user123");
        @event.OccurredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    #endregion

    #region Pause - Invalid Transitions

    [Fact]
    public void Pause_FromPausedByUser_ShouldThrowDomainException()
    {
        // Arrange
        var state = TrackingState.CreateActive();
        state.Pause("First pause", "user123");

        // Act
        var act = () => state.Pause("Second pause", "user123");

        // Assert
        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "INVALID_TRANSITION");
    }

    [Fact]
    public void Pause_FromDisabled_ShouldThrowDomainException()
    {
        // Arrange
        var state = TrackingState.CreateActive();
        state.Disable();

        // Act
        var act = () => state.Pause("Trying to pause", "user123");

        // Assert
        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "INVALID_TRANSITION");
    }

    #endregion

    #region Resume - Valid Transitions

    [Fact]
    public void Resume_FromPausedByUser_ShouldSucceed()
    {
        // Arrange
        var state = TrackingState.CreateActive();
        state.Pause("Break", "user123");

        // Act
        state.Resume("user123");

        // Assert
        state.Status.Should().Be(TrackingStatus.Active);
        state.IsActive.Should().BeTrue();
        state.Reason.Should().BeNull();
        state.ResumedAt.Should().NotBeNull();
        state.LastModifiedBy.Should().Be("user123");
    }

    [Fact]
    public void Resume_FromPausedByPolicy_ShouldSucceed()
    {
        // Arrange
        var state = TrackingState.CreateActive();
        state.Pause("Policy", "system", isPolicy: true);

        // Act
        state.Resume("user123");

        // Assert
        state.Status.Should().Be(TrackingStatus.Active);
    }

    [Fact]
    public void Resume_ShouldRaiseTrackingResumedEvent()
    {
        // Arrange
        var state = TrackingState.CreateActive();
        state.Pause("Break", "user123");
        state.ClearEvents();

        // Act
        state.Resume("user123");

        // Assert
        state.Events.Should().HaveCount(1);
        var @event = state.Events[0].Should().BeOfType<TrackingResumed>().Subject;
        @event.ResumedBy.Should().Be("user123");
        @event.OccurredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    #endregion

    #region Resume - Invalid Transitions

    [Fact]
    public void Resume_FromActive_ShouldThrowDomainException()
    {
        // Arrange
        var state = TrackingState.CreateActive();

        // Act
        var act = () => state.Resume("user123");

        // Assert
        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "INVALID_TRANSITION");
    }

    [Fact]
    public void Resume_FromDisabled_ShouldThrowDomainException()
    {
        // Arrange
        var state = TrackingState.CreateActive();
        state.Disable();

        // Act
        var act = () => state.Resume("user123");

        // Assert
        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "INVALID_TRANSITION");
    }

    #endregion

    #region Disable

    [Fact]
    public void Disable_FromActive_ShouldSucceed()
    {
        // Arrange
        var state = TrackingState.CreateActive();

        // Act
        state.Disable("Maintenance");

        // Assert
        state.Status.Should().Be(TrackingStatus.Disabled);
        state.IsDisabled.Should().BeTrue();
        state.Reason.Should().Be("Maintenance");
    }

    [Fact]
    public void Disable_FromPausedByUser_ShouldSucceed()
    {
        // Arrange
        var state = TrackingState.CreateActive();
        state.Pause("Break", "user123");

        // Act
        state.Disable();

        // Assert
        state.Status.Should().Be(TrackingStatus.Disabled);
    }

    [Fact]
    public void Disable_FromPausedByPolicy_ShouldSucceed()
    {
        // Arrange
        var state = TrackingState.CreateActive();
        state.Pause("Policy", "system", isPolicy: true);

        // Act
        state.Disable();

        // Assert
        state.Status.Should().Be(TrackingStatus.Disabled);
    }

    [Fact]
    public void Disable_FromDisabled_ShouldThrowDomainException()
    {
        // Arrange
        var state = TrackingState.CreateActive();
        state.Disable();

        // Act
        var act = () => state.Disable();

        // Assert
        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "INVALID_TRANSITION");
    }

    #endregion

    #region Enable

    [Fact]
    public void Enable_FromDisabled_ShouldSucceed()
    {
        // Arrange
        var state = TrackingState.CreateActive();
        state.Disable();

        // Act
        state.Enable();

        // Assert
        state.Status.Should().Be(TrackingStatus.Active);
        state.IsActive.Should().BeTrue();
        state.Reason.Should().BeNull();
        state.PausedAt.Should().BeNull();
        state.ResumedAt.Should().BeNull();
    }

    [Fact]
    public void Enable_FromActive_ShouldThrowDomainException()
    {
        // Arrange
        var state = TrackingState.CreateActive();

        // Act
        var act = () => state.Enable();

        // Assert
        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "INVALID_TRANSITION");
    }

    [Fact]
    public void Enable_FromPaused_ShouldThrowDomainException()
    {
        // Arrange
        var state = TrackingState.CreateActive();
        state.Pause("Break", "user123");

        // Act
        var act = () => state.Enable();

        // Assert
        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "INVALID_TRANSITION");
    }

    #endregion

    #region Events

    [Fact]
    public void ClearEvents_ShouldRemoveAllEvents()
    {
        // Arrange
        var state = TrackingState.CreateActive();
        state.Pause("Break", "user123");
        state.Events.Should().HaveCount(1);

        // Act
        state.ClearEvents();

        // Assert
        state.Events.Should().BeEmpty();
    }

    [Fact]
    public void MultipleTransitions_ShouldAccumulateEvents()
    {
        // Arrange
        var state = TrackingState.CreateActive();

        // Act
        state.Pause("Break 1", "user123");
        state.Resume("user123");
        state.Pause("Break 2", "user123");

        // Assert
        state.Events.Should().HaveCount(3);
        state.Events[0].Should().BeOfType<TrackingPaused>();
        state.Events[1].Should().BeOfType<TrackingResumed>();
        state.Events[2].Should().BeOfType<TrackingPaused>();
    }

    #endregion

    #region Serialization

    [Fact]
    public void ToDto_And_FromDto_ShouldPreserveState()
    {
        // Arrange
        var original = TrackingState.CreateActive();
        original.Pause("Break", "user123", isPolicy: false);

        // Act
        var dto = original.ToDto();
        var restored = TrackingState.FromDto(dto);

        // Assert
        restored.Id.Should().Be(original.Id);
        restored.Status.Should().Be(original.Status);
        restored.Reason.Should().Be(original.Reason);
        restored.PausedAt.Should().Be(original.PausedAt);
        restored.ResumedAt.Should().Be(original.ResumedAt);
        restored.UpdatedAt.Should().Be(original.UpdatedAt);
        restored.LastModifiedBy.Should().Be(original.LastModifiedBy);
    }

    [Fact]
    public void Serialization_ActiveState_ShouldPreserveAllFields()
    {
        // Arrange
        var original = TrackingState.CreateActive();

        // Act
        var dto = original.ToDto();
        var restored = TrackingState.FromDto(dto);

        // Assert
        restored.Status.Should().Be(TrackingStatus.Active);
        restored.Reason.Should().BeNull();
        restored.PausedAt.Should().BeNull();
        restored.ResumedAt.Should().BeNull();
    }

    [Fact]
    public void Serialization_DisabledState_ShouldPreserveAllFields()
    {
        // Arrange
        var original = TrackingState.CreateActive();
        original.Disable("Maintenance mode");

        // Act
        var dto = original.ToDto();
        var restored = TrackingState.FromDto(dto);

        // Assert
        restored.Status.Should().Be(TrackingStatus.Disabled);
        restored.Reason.Should().Be("Maintenance mode");
    }

    #endregion
}
