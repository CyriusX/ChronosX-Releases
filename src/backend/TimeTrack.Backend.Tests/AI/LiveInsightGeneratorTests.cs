using FluentAssertions;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Services;
using TimeTrack.Backend.Domain.ValueObjects;
using Xunit;

namespace TimeTrack.Backend.Tests.AI;

public sealed class LiveInsightGeneratorTests
{
    private readonly LiveInsightGenerator _sut = new();

    private static ActivitySession CreateSession(
        Guid userId, Guid orgId,
        string processName, string? appCategory,
        int durationSeconds,
        DateTime? startedAt = null)
    {
        var start = startedAt ?? DateTime.UtcNow.AddMinutes(-30);
        var end = start.AddSeconds(durationSeconds);

        return ActivitySession.Create(
            Guid.NewGuid(), orgId, Guid.NewGuid(), userId,
            processName, "Window Title", appCategory,
            start, end, Guid.NewGuid().ToString());
    }

    [Fact]
    public void ComputeMetrics_WithProductiveSessions_ReturnsCorrectMetrics()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        var sessions = new List<ActivitySession>
        {
            CreateSession(userId, orgId, "code", "productive", 1800),
            CreateSession(userId, orgId, "code", "productive", 1800),
            CreateSession(userId, orgId, "chrome", "neutral", 600),
        };

        var metrics = _sut.ComputeMetrics(sessions);

        metrics.ProductiveMs.Should().Be(3600 * 1000L);
        metrics.NeutralMs.Should().Be(600 * 1000L);
        metrics.DistractionMs.Should().Be(0);
        metrics.TotalMs.Should().Be(4200 * 1000L);
    }

    [Fact]
    public void ComputeMetrics_WithDistractionTracksTopApp()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        var sessions = new List<ActivitySession>
        {
            CreateSession(userId, orgId, "youtube", "distraction", 1800),
            CreateSession(userId, orgId, "reddit", "distraction", 600),
            CreateSession(userId, orgId, "code", "productive", 1200),
        };

        var metrics = _sut.ComputeMetrics(sessions);

        metrics.TopDistractionApp.Should().NotBeNull();
        metrics.DistractionMs.Should().Be(2400 * 1000L);
    }

    [Fact]
    public void ComputeMetrics_TracksContextSwitches()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var baseTime = DateTime.UtcNow.AddMinutes(-60);

        var sessions = new List<ActivitySession>
        {
            CreateSession(userId, orgId, "code", "productive", 600, startedAt: baseTime),
            CreateSession(userId, orgId, "chrome", "productive", 600, startedAt: baseTime.AddSeconds(600)),
            CreateSession(userId, orgId, "code", "productive", 600, startedAt: baseTime.AddSeconds(1200)),
        };

        var metrics = _sut.ComputeMetrics(sessions);

        metrics.ContextSwitches.Should().Be(2); // code→chrome→code
    }

    [Fact]
    public void ComputeMetrics_TracksLongestFocusBlock()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var baseTime = DateTime.UtcNow.AddMinutes(-120);

        var sessions = new List<ActivitySession>
        {
            CreateSession(userId, orgId, "code", "productive", 1800, startedAt: baseTime),
            CreateSession(userId, orgId, "youtube", "distraction", 300, startedAt: baseTime.AddSeconds(1800)),
            CreateSession(userId, orgId, "code", "productive", 3600, startedAt: baseTime.AddSeconds(2100)),
        };

        var metrics = _sut.ComputeMetrics(sessions);

        metrics.LongestFocusBlockMs.Should().Be(3600 * 1000L);
    }

    [Fact]
    public void DetectConditions_WhenHighDistraction_ReturnsDistractionAlert()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        // 60% distraction
        var metrics = new LiveMetrics
        {
            ProductiveMs = 1800 * 1000L,
            DistractionMs = 2700 * 1000L,
            NeutralMs = 0,
            TotalMs = 4500 * 1000L,
            ContextSwitches = 5,
            RecentContextSwitches = 3,
            LongestFocusBlockMs = 1800 * 1000L,
            TopDistractionApp = "YouTube",
            TopDistractionMs = 2000 * 1000L,
        };

        var thresholds = new LiveInsightThresholds
        {
            DistractionThreshold = 0.4,
            OverworkMinutesThreshold = 120,
            DeepFocusMinutesThreshold = 45,
        };

        var results = _sut.DetectConditions(userId, orgId, metrics, thresholds);

        results.Should().Contain(c => c.AlertType == "live_distraction");
    }

    [Fact]
    public void DetectConditions_WhenOverwork_ReturnsOverworkAlert()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        // 3h of active time (> 120min threshold)
        var metrics = new LiveMetrics
        {
            ProductiveMs = 3 * 3600 * 1000L,
            DistractionMs = 0,
            NeutralMs = 0,
            TotalMs = 3 * 3600 * 1000L,
            ContextSwitches = 5,
            RecentContextSwitches = 3,
            LongestFocusBlockMs = 1800 * 1000L,
            TopDistractionApp = null,
            TopDistractionMs = 0,
        };

        var thresholds = new LiveInsightThresholds
        {
            DistractionThreshold = 0.4,
            OverworkMinutesThreshold = 120,
            DeepFocusMinutesThreshold = 45,
        };

        var results = _sut.DetectConditions(userId, orgId, metrics, thresholds);

        results.Should().Contain(c => c.AlertType == "live_overwork");
    }

    [Fact]
    public void DetectConditions_WhenDeepFocus_ReturnsDeepFocusInsight()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        // 60min focus block (> 45min threshold)
        var metrics = new LiveMetrics
        {
            ProductiveMs = 3600 * 1000L,
            DistractionMs = 0,
            NeutralMs = 0,
            TotalMs = 3600 * 1000L,
            ContextSwitches = 0,
            RecentContextSwitches = 0,
            LongestFocusBlockMs = 60 * 60 * 1000L,
            TopDistractionApp = null,
            TopDistractionMs = 0,
        };

        var thresholds = new LiveInsightThresholds
        {
            DistractionThreshold = 0.4,
            OverworkMinutesThreshold = 120,
            DeepFocusMinutesThreshold = 45,
        };

        var results = _sut.DetectConditions(userId, orgId, metrics, thresholds);

        results.Should().Contain(c => c.AlertType == "live_deep_focus");
    }

    [Fact]
    public void DetectConditions_WhenZeroTotalMs_ReturnsEmpty()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        var metrics = new LiveMetrics
        {
            ProductiveMs = 0,
            DistractionMs = 0,
            NeutralMs = 0,
            TotalMs = 0,
            ContextSwitches = 0,
            RecentContextSwitches = 0,
            LongestFocusBlockMs = 0,
            TopDistractionApp = null,
            TopDistractionMs = 0,
        };

        var thresholds = new LiveInsightThresholds();

        var results = _sut.DetectConditions(userId, orgId, metrics, thresholds);

        results.Should().BeEmpty();
    }

    [Fact]
    public void DetectConditions_WhenMilestoneReached_ReturnsMilestoneInsight()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        // 2h productive = exactly 2.0 (milestone)
        var metrics = new LiveMetrics
        {
            ProductiveMs = 2 * 3600 * 1000L,
            DistractionMs = 1800 * 1000L,
            NeutralMs = 0,
            TotalMs = (2 * 3600 + 1800) * 1000L,
            ContextSwitches = 5,
            RecentContextSwitches = 3,
            LongestFocusBlockMs = 1800 * 1000L,
            TopDistractionApp = null,
            TopDistractionMs = 0,
        };

        var thresholds = new LiveInsightThresholds
        {
            DistractionThreshold = 0.8, // high enough to not trigger distraction
            OverworkMinutesThreshold = 300,
            DeepFocusMinutesThreshold = 45,
        };

        var results = _sut.DetectConditions(userId, orgId, metrics, thresholds);

        results.Should().Contain(c => c.AlertType == "live_milestone");
    }

}
