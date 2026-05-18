using FluentAssertions;
using TimeTrack.Backend.Domain.Entities;
using Xunit;

namespace TimeTrack.Backend.Tests.AI;

public sealed class SmartAlertTests
{
    [Fact]
    public void Create_SetsAllProperties()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var aboutUserId = Guid.NewGuid();

        var alert = SmartAlert.Create(userId, orgId, "overwork", "Voce esta ha 3h sem pausa.", "info",
            actionType: null, aboutUserId: aboutUserId);

        alert.Id.Should().NotBe(Guid.Empty);
        alert.UserId.Should().Be(userId);
        alert.AlertType.Should().Be("overwork");
        alert.Message.Should().Be("Voce esta ha 3h sem pausa.");
        alert.AboutUserId.Should().Be(aboutUserId);
        alert.WasRead.Should().BeFalse();
        alert.WasActed.Should().BeFalse();
    }

    [Fact]
    public void Create_WithActionType_SetsActionType()
    {
        var alert = SmartAlert.Create(Guid.NewGuid(), Guid.NewGuid(), "focus_drop", "msg", "warning",
            actionType: "start_pomodoro");

        alert.ActionType.Should().Be("start_pomodoro");
    }

    [Fact]
    public void MarkRead_SetsWasRead()
    {
        var alert = SmartAlert.Create(Guid.NewGuid(), Guid.NewGuid(), "overwork", "msg", "info");
        alert.MarkRead();
        alert.WasRead.Should().BeTrue();
    }

    [Fact]
    public void MarkActed_SetsWasActed()
    {
        var alert = SmartAlert.Create(Guid.NewGuid(), Guid.NewGuid(), "focus_drop", "msg", "warning");
        alert.MarkActed();
        alert.WasActed.Should().BeTrue();
    }

    // === TryCreate*() detection method tests ===

    private static DailyFocusScore CreateScore(
        Guid userId, Guid orgId, DateOnly date,
        int productiveSeconds, int distractionSeconds, int neutralSeconds,
        short focusScore, int contextSwitchesCount = 0)
    {
        var totalMs = (productiveSeconds + distractionSeconds + neutralSeconds) * 1000L;
        var score = DailyFocusScore.Create(
            Guid.NewGuid(), orgId, userId, date,
            totalMs, productiveSeconds * 1000L, distractionSeconds * 1000L,
            0, 0, 0, 0, focusScore);

        score.UpdateFeatures(
            productiveSeconds, distractionSeconds, neutralSeconds,
            contextSwitchesCount, 0, null, 0, 0, 0, 0, 0, 0, 0);

        return score;
    }

    [Fact]
    public void TryCreateOverworkAlert_WhenAboveThreshold_ReturnsAlert()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

        // 4h active = 14400s, above 3h (10800s) threshold
        var score = CreateScore(userId, orgId, date,
            productiveSeconds: 14400, distractionSeconds: 0, neutralSeconds: 0,
            focusScore: 80);

        var result = SmartAlert.TryCreateOverworkAlert(score, overworkThresholdSeconds: 10800);

        result.Should().NotBeNull();
        result!.AlertType.Should().Be("overwork");
        result.Severity.Should().Be("info");
        result.UserId.Should().Be(userId);
        result.OrgId.Should().Be(orgId);
        result.Message.Should().Contain("h");
        result.Message.Should().Contain("ativo");
    }

    [Fact]
    public void TryCreateOverworkAlert_WhenBelowThreshold_ReturnsNull()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

        // 2h active = 7200s, below 3h (10800s) threshold
        var score = CreateScore(userId, orgId, date,
            productiveSeconds: 7200, distractionSeconds: 0, neutralSeconds: 0,
            focusScore: 80);

        var result = SmartAlert.TryCreateOverworkAlert(score, overworkThresholdSeconds: 10800);

        result.Should().BeNull();
    }

    [Fact]
    public void TryCreateOverworkAlert_WhenJustBelowThreshold_ReturnsNull()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

        // 1 second below threshold
        var score = CreateScore(userId, orgId, date,
            productiveSeconds: 10799, distractionSeconds: 0, neutralSeconds: 0,
            focusScore: 80);

        var result = SmartAlert.TryCreateOverworkAlert(score, overworkThresholdSeconds: 10800);

        result.Should().BeNull();
    }

    [Fact]
    public void TryCreateFocusDropAlert_WhenBelowPersonalThreshold_ReturnsAlert()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

        // Score = 20, baseline = 80, multiplier = 0.6 → threshold = 48
        var score = CreateScore(userId, orgId, date,
            productiveSeconds: 28800, distractionSeconds: 0, neutralSeconds: 0,
            focusScore: 20);

        var result = SmartAlert.TryCreateFocusDropAlert(score, baselineAvgFocus: 80, focusDropMultiplier: 0.6);

        result.Should().NotBeNull();
        result!.AlertType.Should().Be("focus_drop");
        result.Severity.Should().Be("warning");
        result.ActionType.Should().Be("start_pomodoro");
        result.Message.Should().Contain("focus");
        result.Message.Should().Contain("80");
    }

    [Fact]
    public void TryCreateFocusDropAlert_WhenScoreAboveThreshold_ReturnsNull()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

        // Score = 60, baseline = 80, multiplier = 0.6 → threshold = 48 → 60 >= 48, no alert
        var score = CreateScore(userId, orgId, date,
            productiveSeconds: 28800, distractionSeconds: 0, neutralSeconds: 0,
            focusScore: 60);

        var result = SmartAlert.TryCreateFocusDropAlert(score, baselineAvgFocus: 80, focusDropMultiplier: 0.6);

        result.Should().BeNull();
    }
}
