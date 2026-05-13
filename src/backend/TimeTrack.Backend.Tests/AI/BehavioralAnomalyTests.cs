using FluentAssertions;
using TimeTrack.Backend.Domain.Entities;
using Xunit;

namespace TimeTrack.Backend.Tests.AI;

public sealed class BehavioralAnomalyTests
{
    private static System.Text.Json.JsonElement TestEvidence =>
        System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>("{\"key\": \"value\"}");

    [Fact]
    public void Create_SetsAllProperties()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        var anomaly = BehavioralAnomaly.Create(userId, orgId, "productivity_drop", "warning", date,
            TestEvidence, baselineValue: 0.7, actualValue: 0.3);

        anomaly.Id.Should().NotBe(Guid.Empty);
        anomaly.UserId.Should().Be(userId);
        anomaly.AnomalyType.Should().Be("productivity_drop");
        anomaly.Severity.Should().Be("warning");
        anomaly.BaselineValue.Should().Be(0.7);
        anomaly.ActualValue.Should().Be(0.3);
    }

    [Fact]
    public void Create_WithoutOptionalValues_SetsNull()
    {
        var anomaly = BehavioralAnomaly.Create(Guid.NewGuid(), Guid.NewGuid(), "absent_workday", "info",
            DateOnly.MaxValue, TestEvidence);

        anomaly.BaselineValue.Should().BeNull();
        anomaly.ActualValue.Should().BeNull();
    }

    [Theory]
    [InlineData("productivity_drop", "warning")]
    [InlineData("absent_workday", "info")]
    [InlineData("exceptionally_long_day", "info")]
    [InlineData("focus_collapse", "alert")]
    public void Create_WithDifferentTypes_SetsCorrectSeverity(string anomalyType, string expectedSeverity)
    {
        var anomaly = BehavioralAnomaly.Create(Guid.NewGuid(), Guid.NewGuid(), anomalyType, expectedSeverity,
            DateOnly.MaxValue, TestEvidence);

        anomaly.Severity.Should().Be(expectedSeverity);
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
    public void TryCreateProductivityDrop_WhenBelowThreshold_ReturnsAnomaly()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var baselineDate = today.AddDays(-15);

        // 14 days of baseline with high productivity (~90%)
        var baseline = Enumerable.Range(0, 14)
            .Select(i => CreateScore(userId, orgId, baselineDate.AddDays(i),
                productiveSeconds: 25000, distractionSeconds: 2000, neutralSeconds: 1800,
                focusScore: 80, contextSwitchesCount: 5))
            .ToList();

        // Yesterday: severe productivity drop (~7%)
        var yesterday = CreateScore(userId, orgId, today.AddDays(-1),
            productiveSeconds: 2000, distractionSeconds: 25000, neutralSeconds: 1800,
            focusScore: 20, contextSwitchesCount: 50);

        var result = BehavioralAnomaly.TryCreateProductivityDrop(
            userId, orgId, today, baseline, yesterday,
            sigmaMultiplier: 2.0, minBaselineDays: 14);

        result.Should().NotBeNull();
        result!.AnomalyType.Should().Be("productivity_drop");
        result.Severity.Should().Be("warning");
        result.BaselineValue.Should().BeGreaterThan(0);
        result.ActualValue.Should().BeLessThan(result.BaselineValue!.Value);
    }

    [Fact]
    public void TryCreateProductivityDrop_WhenWithinNormalRange_ReturnsNull()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var baselineDate = today.AddDays(-15);

        // Baseline with variation to generate meaningful stddev
        var baseline = new List<DailyFocusScore>
        {
            CreateScore(userId, orgId, baselineDate.AddDays(0), 20000, 5000, 3800, 75),
            CreateScore(userId, orgId, baselineDate.AddDays(1), 25000, 2000, 1800, 85),
            CreateScore(userId, orgId, baselineDate.AddDays(2), 22000, 4000, 2800, 78),
            CreateScore(userId, orgId, baselineDate.AddDays(3), 24000, 3000, 1800, 82),
            CreateScore(userId, orgId, baselineDate.AddDays(4), 21000, 4500, 3300, 76),
            CreateScore(userId, orgId, baselineDate.AddDays(5), 26000, 1500, 1300, 88),
            CreateScore(userId, orgId, baselineDate.AddDays(6), 23000, 3500, 2300, 80),
            CreateScore(userId, orgId, baselineDate.AddDays(7), 20000, 5000, 3800, 75),
            CreateScore(userId, orgId, baselineDate.AddDays(8), 25000, 2000, 1800, 85),
            CreateScore(userId, orgId, baselineDate.AddDays(9), 22000, 4000, 2800, 78),
            CreateScore(userId, orgId, baselineDate.AddDays(10), 24000, 3000, 1800, 82),
            CreateScore(userId, orgId, baselineDate.AddDays(11), 21000, 4500, 3300, 76),
            CreateScore(userId, orgId, baselineDate.AddDays(12), 26000, 1500, 1300, 88),
            CreateScore(userId, orgId, baselineDate.AddDays(13), 23000, 3500, 2300, 80),
        };

        // Yesterday: normal productivity within 2 sigma
        var yesterday = CreateScore(userId, orgId, today.AddDays(-1),
            productiveSeconds: 23000, distractionSeconds: 3500, neutralSeconds: 2300,
            focusScore: 80);

        var result = BehavioralAnomaly.TryCreateProductivityDrop(
            userId, orgId, today, baseline, yesterday,
            sigmaMultiplier: 2.0, minBaselineDays: 14);

        result.Should().BeNull();
    }

    [Fact]
    public void TryCreateProductivityDrop_WhenInsufficientBaseline_ReturnsNull()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Only 5 days — below minimum of 14
        var baseline = Enumerable.Range(0, 5)
            .Select(i => CreateScore(userId, orgId, today.AddDays(-20 + i),
                productiveSeconds: 28800, distractionSeconds: 0, neutralSeconds: 0,
                focusScore: 80))
            .ToList();

        var yesterday = CreateScore(userId, orgId, today.AddDays(-1),
            productiveSeconds: 2000, distractionSeconds: 25000, neutralSeconds: 1800,
            focusScore: 20);

        var result = BehavioralAnomaly.TryCreateProductivityDrop(
            userId, orgId, today, baseline, yesterday,
            sigmaMultiplier: 2.0, minBaselineDays: 14);

        result.Should().BeNull();
    }

    [Fact]
    public void TryCreateAbsentWorkday_WhenWeekdayWithLowActivity_ReturnsAnomaly()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        // Find a weekday
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            date = date.AddDays(-1);

        var yesterday = CreateScore(userId, orgId, date,
            productiveSeconds: 600, distractionSeconds: 0, neutralSeconds: 0,
            focusScore: 10);

        var result = BehavioralAnomaly.TryCreateAbsentWorkday(
            userId, orgId, date.AddDays(1), yesterday,
            minimumWorkdaySeconds: 3600);

        result.Should().NotBeNull();
        result!.AnomalyType.Should().Be("absent_workday");
        result.Severity.Should().Be("info");
    }

    [Fact]
    public void TryCreateAbsentWorkday_WhenWeekend_ReturnsNull()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        // Find a weekend day
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        while (date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
            date = date.AddDays(-1);

        var yesterday = CreateScore(userId, orgId, date,
            productiveSeconds: 600, distractionSeconds: 0, neutralSeconds: 0,
            focusScore: 10);

        var result = BehavioralAnomaly.TryCreateAbsentWorkday(
            userId, orgId, date.AddDays(1), yesterday,
            minimumWorkdaySeconds: 3600);

        result.Should().BeNull();
    }

    [Fact]
    public void TryCreateAbsentWorkday_WhenSufficientActivity_ReturnsNull()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            date = date.AddDays(-1);

        var yesterday = CreateScore(userId, orgId, date,
            productiveSeconds: 14400, distractionSeconds: 0, neutralSeconds: 0,
            focusScore: 80);

        var result = BehavioralAnomaly.TryCreateAbsentWorkday(
            userId, orgId, date.AddDays(1), yesterday,
            minimumWorkdaySeconds: 3600);

        result.Should().BeNull();
    }

    [Fact]
    public void TryCreateLongDay_WhenExceptionallyLong_ReturnsAnomaly()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var baselineDate = today.AddDays(-15);

        // Baseline: ~8h days
        var baseline = Enumerable.Range(0, 14)
            .Select(i => CreateScore(userId, orgId, baselineDate.AddDays(i),
                productiveSeconds: 28800, distractionSeconds: 0, neutralSeconds: 0,
                focusScore: 80))
            .ToList();

        // Yesterday: 16h day (> 1.8x baseline avg of 28800)
        var yesterday = CreateScore(userId, orgId, today.AddDays(-1),
            productiveSeconds: 57600, distractionSeconds: 0, neutralSeconds: 0,
            focusScore: 70);

        var result = BehavioralAnomaly.TryCreateLongDay(
            userId, orgId, today, yesterday, baseline,
            longDayMultiplier: 1.8);

        result.Should().NotBeNull();
        result!.AnomalyType.Should().Be("exceptionally_long_day");
        result.Severity.Should().Be("info");
        result.ActualValue.Should().BeGreaterThan(result.BaselineValue!.Value);
    }

    [Fact]
    public void TryCreateLongDay_WhenNormalDay_ReturnsNull()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var baselineDate = today.AddDays(-15);

        var baseline = Enumerable.Range(0, 14)
            .Select(i => CreateScore(userId, orgId, baselineDate.AddDays(i),
                productiveSeconds: 28800, distractionSeconds: 0, neutralSeconds: 0,
                focusScore: 80))
            .ToList();

        // Yesterday: normal 8h day
        var yesterday = CreateScore(userId, orgId, today.AddDays(-1),
            productiveSeconds: 28800, distractionSeconds: 0, neutralSeconds: 0,
            focusScore: 80);

        var result = BehavioralAnomaly.TryCreateLongDay(
            userId, orgId, today, yesterday, baseline,
            longDayMultiplier: 1.8);

        result.Should().BeNull();
    }

    [Fact]
    public void TryCreateFocusCollapse_WhenThreeConsecutiveLowDays_ReturnsAnomaly()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Baseline with focus score 80
        var baseline = Enumerable.Range(0, 14)
            .Select(i => CreateScore(userId, orgId, today.AddDays(-30 + i),
                productiveSeconds: 28800, distractionSeconds: 0, neutralSeconds: 0,
                focusScore: 80))
            .ToList();

        // Recent 3 days with focus score 20 (below 80 * 0.5 = 40 threshold)
        var recentScores = new List<DailyFocusScore>(baseline);
        recentScores.Add(CreateScore(userId, orgId, today.AddDays(-3),
            productiveSeconds: 28800, distractionSeconds: 0, neutralSeconds: 0,
            focusScore: 20));
        recentScores.Add(CreateScore(userId, orgId, today.AddDays(-2),
            productiveSeconds: 28800, distractionSeconds: 0, neutralSeconds: 0,
            focusScore: 15));
        recentScores.Add(CreateScore(userId, orgId, today.AddDays(-1),
            productiveSeconds: 28800, distractionSeconds: 0, neutralSeconds: 0,
            focusScore: 25));

        var result = BehavioralAnomaly.TryCreateFocusCollapse(
            userId, orgId, today, recentScores, baseline,
            focusCollapseRatio: 0.5);

        result.Should().NotBeNull();
        result!.AnomalyType.Should().Be("focus_collapse");
        result.Severity.Should().Be("alert");
    }

    [Fact]
    public void TryCreateFocusCollapse_WhenNotAllThreeDaysLow_ReturnsNull()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var baseline = Enumerable.Range(0, 14)
            .Select(i => CreateScore(userId, orgId, today.AddDays(-30 + i),
                productiveSeconds: 28800, distractionSeconds: 0, neutralSeconds: 0,
                focusScore: 80))
            .ToList();

        var recentScores = new List<DailyFocusScore>(baseline);
        recentScores.Add(CreateScore(userId, orgId, today.AddDays(-3),
            productiveSeconds: 28800, distractionSeconds: 0, neutralSeconds: 0,
            focusScore: 20));
        recentScores.Add(CreateScore(userId, orgId, today.AddDays(-2),
            productiveSeconds: 28800, distractionSeconds: 0, neutralSeconds: 0,
            focusScore: 15));
        // Day 3 has recovered focus
        recentScores.Add(CreateScore(userId, orgId, today.AddDays(-1),
            productiveSeconds: 28800, distractionSeconds: 0, neutralSeconds: 0,
            focusScore: 85));

        var result = BehavioralAnomaly.TryCreateFocusCollapse(
            userId, orgId, today, recentScores, baseline,
            focusCollapseRatio: 0.5);

        result.Should().BeNull();
    }

    [Fact]
    public void TryCreateFocusCollapse_WhenFewerThanThreeDays_ReturnsNull()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var baseline = Enumerable.Range(0, 14)
            .Select(i => CreateScore(userId, orgId, today.AddDays(-30 + i),
                productiveSeconds: 28800, distractionSeconds: 0, neutralSeconds: 0,
                focusScore: 80))
            .ToList();

        // Only 2 recent scores
        var recentScores = new List<DailyFocusScore>(baseline);
        recentScores.Add(CreateScore(userId, orgId, today.AddDays(-2),
            productiveSeconds: 28800, distractionSeconds: 0, neutralSeconds: 0,
            focusScore: 20));
        recentScores.Add(CreateScore(userId, orgId, today.AddDays(-1),
            productiveSeconds: 28800, distractionSeconds: 0, neutralSeconds: 0,
            focusScore: 15));

        var result = BehavioralAnomaly.TryCreateFocusCollapse(
            userId, orgId, today, recentScores, baseline,
            focusCollapseRatio: 0.5);

        result.Should().BeNull();
    }
}
