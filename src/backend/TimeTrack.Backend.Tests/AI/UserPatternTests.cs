using System.Text.Json;
using FluentAssertions;
using TimeTrack.Backend.Domain.Entities;
using Xunit;

namespace TimeTrack.Backend.Tests.AI;

public sealed class UserPatternTests
{
    private static JsonElement TestEvidence => JsonSerializer.Deserialize<JsonElement>("{\"test\": true}");

    [Fact]
    public void Create_SetsAllProperties()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var pattern = UserPattern.Create(userId, orgId, "morning_productive", today, 0.8, TestEvidence, "desc");

        pattern.Id.Should().NotBe(Guid.Empty);
        pattern.UserId.Should().Be(userId);
        pattern.PatternTag.Should().Be("morning_productive");
        pattern.Strength.Should().Be(0.8);
        pattern.Description.Should().Be("desc");
        pattern.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var pattern = UserPattern.Create(Guid.NewGuid(), Guid.NewGuid(), "tag", DateOnly.MaxValue, 0.5, TestEvidence);
        pattern.Deactivate();
        pattern.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Reactivate_SetsIsActiveTrueWithNewValues()
    {
        var pattern = UserPattern.Create(Guid.NewGuid(), Guid.NewGuid(), "tag", DateOnly.MaxValue, 0.5, TestEvidence);
        pattern.Deactivate();
        var newEvidence = JsonSerializer.Deserialize<JsonElement>("{\"updated\": true}");
        pattern.Reactivate(0.9, newEvidence);
        pattern.IsActive.Should().BeTrue();
        pattern.Strength.Should().Be(0.9);
    }

    [Fact]
    public void SetDescription_UpdatesDescription()
    {
        var pattern = UserPattern.Create(Guid.NewGuid(), Guid.NewGuid(), "tag", DateOnly.MaxValue, 0.5, TestEvidence);
        pattern.SetDescription("Voce e produtivo pela manha.");
        pattern.Description.Should().Be("Voce e produtivo pela manha.");
    }

    // === TryDetect*() detection method tests ===

    private static DailyFocusScore CreateScore(
        Guid userId, Guid orgId, DateOnly date,
        int productiveSeconds, int distractionSeconds, int neutralSeconds,
        short focusScore, int contextSwitchesCount = 0,
        int longestFocusSeconds = 0, double productivityRatio = 0)
    {
        var totalMs = (productiveSeconds + distractionSeconds + neutralSeconds) * 1000L;
        var score = DailyFocusScore.Create(
            Guid.NewGuid(), orgId, userId, date,
            totalMs, productiveSeconds * 1000L, distractionSeconds * 1000L,
            0, 0, 0, 0, focusScore);

        var ratio = productivityRatio > 0
            ? productivityRatio
            : (productiveSeconds + distractionSeconds + neutralSeconds > 0
                ? (double)productiveSeconds / (productiveSeconds + distractionSeconds + neutralSeconds)
                : 0);

        score.UpdateFeatures(
            productiveSeconds, distractionSeconds, neutralSeconds,
            contextSwitchesCount, 0, null, 0, 0, 0, 0, 0,
            longestFocusSeconds, 0);

        // Set productivity ratio via the internal UpdateFeatures calculation
        // (it recalculates based on the passed values)
        return score;
    }

    [Fact]
    public void TryDetectMorningProductive_WhenConsistent_ReturnsCandidate()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 5 workdays (Mon-Fri) with high productivity ratio
        var scores = new List<DailyFocusScore>();
        var startDate = today.AddDays(-7);
        for (var i = 0; i < 7; i++)
        {
            var date = startDate.AddDays(i);
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;

            var score = CreateScore(userId, orgId, date,
                productiveSeconds: 25200, distractionSeconds: 1800, neutralSeconds: 1800,
                focusScore: 80, productivityRatio: 0.88);
            scores.Add(score);
        }

        var result = UserPattern.TryDetectMorningProductive(scores, morningProductiveRatio: 0.7);

        result.Should().NotBeNull();
        result!.Tag.Should().Be("morning_productive");
        result.Strength.Should().BeGreaterThan(0);
    }

    [Fact]
    public void TryDetectMorningProductive_WhenInconsistent_ReturnsNull()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Only 2 workdays — need at least 4
        var scores = new List<DailyFocusScore>();
        var startDate = today.AddDays(-7);
        var workdayCount = 0;
        for (var i = 0; i < 7 && workdayCount < 2; i++)
        {
            var date = startDate.AddDays(i);
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;

            var score = CreateScore(userId, orgId, date,
                productiveSeconds: 25200, distractionSeconds: 1800, neutralSeconds: 1800,
                focusScore: 80, productivityRatio: 0.88);
            scores.Add(score);
            workdayCount++;
        }

        var result = UserPattern.TryDetectMorningProductive(scores, morningProductiveRatio: 0.7);

        result.Should().BeNull();
    }

    [Fact]
    public void TryDetectHighContextSwitching_WhenFrequentSwitcher_ReturnsCandidate()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 4 days with > 30 context switches
        var scores = Enumerable.Range(0, 4)
            .Select(i => CreateScore(userId, orgId, today.AddDays(-7 + i),
                productiveSeconds: 28800, distractionSeconds: 0, neutralSeconds: 0,
                focusScore: 50, contextSwitchesCount: 50))
            .ToList();

        var result = UserPattern.TryDetectHighContextSwitching(scores, contextSwitchThreshold: 30);

        result.Should().NotBeNull();
        result!.Tag.Should().Be("high_context_switching");
    }

    [Fact]
    public void TryDetectHighContextSwitching_WhenBelowThreshold_ReturnsNull()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 4 days with low context switches
        var scores = Enumerable.Range(0, 4)
            .Select(i => CreateScore(userId, orgId, today.AddDays(-7 + i),
                productiveSeconds: 28800, distractionSeconds: 0, neutralSeconds: 0,
                focusScore: 80, contextSwitchesCount: 5))
            .ToList();

        var result = UserPattern.TryDetectHighContextSwitching(scores, contextSwitchThreshold: 30);

        result.Should().BeNull();
    }

    [Fact]
    public void TryDetectDeepWorkCapable_WhenLongFocusBlocks_ReturnsCandidate()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 3 days with > 5400s (90min) longest focus block
        var scores = Enumerable.Range(0, 3)
            .Select(i => CreateScore(userId, orgId, today.AddDays(-7 + i),
                productiveSeconds: 28800, distractionSeconds: 0, neutralSeconds: 0,
                focusScore: 85, contextSwitchesCount: 5,
                longestFocusSeconds: 6000))
            .ToList();

        var result = UserPattern.TryDetectDeepWorkCapable(scores, deepWorkSecondsThreshold: 5400);

        result.Should().NotBeNull();
        result!.Tag.Should().Be("deep_work_capable");
    }

    [Fact]
    public void TryDetectDeepWorkCapable_WhenShortFocusBlocks_ReturnsNull()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 3 days with short focus blocks
        var scores = Enumerable.Range(0, 3)
            .Select(i => CreateScore(userId, orgId, today.AddDays(-7 + i),
                productiveSeconds: 28800, distractionSeconds: 0, neutralSeconds: 0,
                focusScore: 80, contextSwitchesCount: 5,
                longestFocusSeconds: 2000))
            .ToList();

        var result = UserPattern.TryDetectDeepWorkCapable(scores, deepWorkSecondsThreshold: 5400);

        result.Should().BeNull();
    }

    [Fact]
    public void TryDetectHighDistractionRisk_WhenFrequentHighDistraction_ReturnsCandidate()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 4 days with high distraction ratio (> 30%)
        var scores = Enumerable.Range(0, 4)
            .Select(i => CreateScore(userId, orgId, today.AddDays(-7 + i),
                productiveSeconds: 10000, distractionSeconds: 15000, neutralSeconds: 3800,
                focusScore: 30, contextSwitchesCount: 10))
            .ToList();

        var result = UserPattern.TryDetectHighDistractionRisk(scores, distractionRatioThreshold: 0.3);

        result.Should().NotBeNull();
        result!.Tag.Should().Be("high_distraction_risk");
    }

    [Fact]
    public void TryDetectHighDistractionRisk_WhenLowDistraction_ReturnsNull()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 4 days with low distraction
        var scores = Enumerable.Range(0, 4)
            .Select(i => CreateScore(userId, orgId, today.AddDays(-7 + i),
                productiveSeconds: 25000, distractionSeconds: 1000, neutralSeconds: 2800,
                focusScore: 80, contextSwitchesCount: 5))
            .ToList();

        var result = UserPattern.TryDetectHighDistractionRisk(scores, distractionRatioThreshold: 0.3);

        result.Should().BeNull();
    }

    [Fact]
    public void TryDetectAfternoonFocusDrop_WhenConsistentDrop_ReturnsCandidate()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Need avgFocus high enough that avgFocus*0.5 is meaningful
        // 1 high day (focus 95) + 4 low days (focus 20, ratio < 0.4)
        // avgFocus = (95 + 20*4) / 5 = 35, threshold = 35*0.5 = 17.5
        // Low days have focus 20 >= 17.5 — won't trigger
        // So let's use 1 high day + 4 low days differently:
        // 2 high days (focus 90) + 4 low days (focus 15, ratio 0.2)
        // avgFocus = (90*2 + 15*4) / 6 = 40, threshold = 40*0.5 = 20
        // Low days have focus 15 < 20 AND ratio < 0.4 — triggers
        var scores = new List<DailyFocusScore>();

        // 2 high focus days
        scores.Add(CreateScore(userId, orgId, today.AddDays(-7),
            productiveSeconds: 25000, distractionSeconds: 2000, neutralSeconds: 1800,
            focusScore: 90, productivityRatio: 0.84));
        scores.Add(CreateScore(userId, orgId, today.AddDays(-6),
            productiveSeconds: 25000, distractionSeconds: 2000, neutralSeconds: 1800,
            focusScore: 90, productivityRatio: 0.84));

        // 4 low focus days (focus < threshold=20 AND ratio < 0.4)
        for (var i = 0; i < 4; i++)
        {
            scores.Add(CreateScore(userId, orgId, today.AddDays(-5 + i),
                productiveSeconds: 5000, distractionSeconds: 3000, neutralSeconds: 20800,
                focusScore: 15, productivityRatio: 0.17));
        }

        var result = UserPattern.TryDetectAfternoonFocusDrop(scores, afternoonFocusDropMultiplier: 0.5);

        result.Should().NotBeNull();
        result!.Tag.Should().Be("afternoon_focus_drop");
    }
}
