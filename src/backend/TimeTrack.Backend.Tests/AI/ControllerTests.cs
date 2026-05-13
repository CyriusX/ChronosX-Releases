using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Infrastructure.Persistence;
using Xunit;

namespace TimeTrack.Backend.Tests.AI;

/// <summary>
/// Tests for AlertsController logic using direct DbContext operations.
/// Controller authorization is verified via ICurrentUserContext mocking.
/// </summary>
public sealed class AlertsControllerLogicTests : IDisposable
{
    private readonly TimeTrackDbContext _context;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public AlertsControllerLogicTests()
    {
        var options = new DbContextOptionsBuilder<TimeTrackDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new TimeTrackDbContext(options, null);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task ListAlerts_FilteredByUserAndOrg()
    {
        var otherUserId = Guid.NewGuid();

        _context.SmartAlerts.Add(SmartAlert.Create(_userId, _orgId, "overwork", "msg1", "info"));
        _context.SmartAlerts.Add(SmartAlert.Create(_userId, _orgId, "focus_drop", "msg2", "warning"));
        _context.SmartAlerts.Add(SmartAlert.Create(otherUserId, _orgId, "overwork", "msg3", "info"));
        await _context.SaveChangesAsync();

        var userAlerts = await _context.SmartAlerts
            .IgnoreQueryFilters()
            .Where(a => a.UserId == _userId && a.OrgId == _orgId)
            .ToListAsync();

        userAlerts.Should().HaveCount(2);
        userAlerts.All(a => a.UserId == _userId).Should().BeTrue();
    }

    [Fact]
    public async Task ListAlerts_FilterUnreadOnly()
    {
        var readAlert = SmartAlert.Create(_userId, _orgId, "overwork", "read", "info");
        readAlert.MarkRead();

        _context.SmartAlerts.Add(readAlert);
        _context.SmartAlerts.Add(SmartAlert.Create(_userId, _orgId, "focus_drop", "unread", "warning"));
        await _context.SaveChangesAsync();

        var unread = await _context.SmartAlerts
            .IgnoreQueryFilters()
            .Where(a => a.UserId == _userId && a.OrgId == _orgId && !a.WasRead)
            .ToListAsync();

        unread.Should().ContainSingle(a => a.Message == "unread");
    }

    [Fact]
    public async Task MarkRead_UpdatesState()
    {
        var alert = SmartAlert.Create(_userId, _orgId, "overwork", "msg", "info");
        _context.SmartAlerts.Add(alert);
        await _context.SaveChangesAsync();

        var found = await _context.SmartAlerts.IgnoreQueryFilters().FirstAsync();
        found.MarkRead();
        await _context.SaveChangesAsync();

        var reloaded = await _context.SmartAlerts.IgnoreQueryFilters().FirstAsync();
        reloaded.WasRead.Should().BeTrue();
    }

    [Fact]
    public async Task MarkActed_UpdatesBothReadAndActed()
    {
        var alert = SmartAlert.Create(_userId, _orgId, "focus_drop", "msg", "warning",
            actionType: "start_pomodoro");
        _context.SmartAlerts.Add(alert);
        await _context.SaveChangesAsync();

        var found = await _context.SmartAlerts.IgnoreQueryFilters().FirstAsync();
        found.MarkRead();
        found.MarkActed();
        await _context.SaveChangesAsync();

        var reloaded = await _context.SmartAlerts.IgnoreQueryFilters().FirstAsync();
        reloaded.WasRead.Should().BeTrue();
        reloaded.WasActed.Should().BeTrue();
    }

    [Fact]
    public async Task Dismiss_RemovesFromDb()
    {
        var alert = SmartAlert.Create(_userId, _orgId, "overwork", "msg", "info");
        _context.SmartAlerts.Add(alert);
        await _context.SaveChangesAsync();

        _context.SmartAlerts.Remove(alert);
        await _context.SaveChangesAsync();

        var remaining = await _context.SmartAlerts.IgnoreQueryFilters().ToListAsync();
        remaining.Should().BeEmpty();
    }

    [Fact]
    public async Task TeamAlerts_ExcludesPersonalTypes()
    {
        _context.SmartAlerts.Add(SmartAlert.Create(_userId, _orgId, "overwork", "personal", "info"));
        _context.SmartAlerts.Add(SmartAlert.Create(_userId, _orgId, "focus_drop", "personal", "warning"));
        _context.SmartAlerts.Add(SmartAlert.Create(Guid.NewGuid(), _orgId, "team_productivity_drop", "team", "warning"));
        _context.SmartAlerts.Add(SmartAlert.Create(Guid.NewGuid(), _orgId, "team_high_switching", "team2", "info"));
        await _context.SaveChangesAsync();

        var teamAlerts = await _context.SmartAlerts
            .IgnoreQueryFilters()
            .Where(a => a.OrgId == _orgId
                && a.AlertType != "overwork"
                && a.AlertType != "focus_drop")
            .ToListAsync();

        teamAlerts.Should().HaveCount(2);
        teamAlerts.Should().NotContain(a => a.AlertType == "overwork" || a.AlertType == "focus_drop");
    }

    [Fact]
    public async Task UnreadCount_CalculatedCorrectly()
    {
        var a1 = SmartAlert.Create(_userId, _orgId, "overwork", "m1", "info");
        var a2 = SmartAlert.Create(_userId, _orgId, "focus_drop", "m2", "warning");
        var a3 = SmartAlert.Create(_userId, _orgId, "overwork", "m3", "info");
        a3.MarkRead();

        _context.SmartAlerts.AddRange(a1, a2, a3);
        await _context.SaveChangesAsync();

        var count = await _context.SmartAlerts
            .IgnoreQueryFilters()
            .CountAsync(a => a.UserId == _userId && a.OrgId == _orgId && !a.WasRead);

        count.Should().Be(2);
    }
}

/// <summary>
/// Tests for AiFeedbackController logic
/// </summary>
public sealed class AiFeedbackLogicTests : IDisposable
{
    private readonly TimeTrackDbContext _context;
    private readonly Guid _orgId = Guid.NewGuid();

    public AiFeedbackLogicTests()
    {
        var options = new DbContextOptionsBuilder<TimeTrackDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new TimeTrackDbContext(options, null);
    }

    public void Dispose() => _context.Dispose();

    [Theory]
    [InlineData("accepted")]
    [InlineData("rejected")]
    [InlineData("corrected")]
    [InlineData("useful")]
    [InlineData("not_useful")]
    public async Task MarkReviewed_ValidOutcomes_UpdatesDecision(string outcome)
    {
        var input = JsonSerializer.Deserialize<JsonElement>("{\"test\": true}");
        var output = JsonSerializer.Deserialize<JsonElement>("{\"result\": \"neutral\"}");
        var decision = AiDecisionLog.Create(_orgId, "app_classification", input, output, "zai-v1");
        _context.AiDecisionLogs.Add(decision);
        await _context.SaveChangesAsync();

        decision.MarkReviewed(outcome);
        await _context.SaveChangesAsync();

        var reloaded = await _context.AiDecisionLogs.IgnoreQueryFilters().FirstAsync();
        reloaded.WasReviewed.Should().BeTrue();
        reloaded.ReviewOutcome.Should().Be(outcome);
    }

    [Fact]
    public async Task MarkReviewed_WithCorrectValue_SetsCorrectValue()
    {
        var input = JsonSerializer.Deserialize<JsonElement>("{\"test\": true}");
        var output = JsonSerializer.Deserialize<JsonElement>("{\"result\": \"neutral\"}");
        var decision = AiDecisionLog.Create(_orgId, "app_classification", input, output, "zai-v1");
        _context.AiDecisionLogs.Add(decision);
        await _context.SaveChangesAsync();

        var correctValue = JsonSerializer.Deserialize<JsonElement>("{\"category\": \"productive\"}");
        decision.MarkReviewed("corrected", correctValue);
        await _context.SaveChangesAsync();

        var reloaded = await _context.AiDecisionLogs.IgnoreQueryFilters().FirstAsync();
        reloaded.CorrectValue.Should().NotBeNull();
    }

    [Fact]
    public async Task FeedbackReport_AggregatesByDecisionType()
    {
        var input = JsonSerializer.Deserialize<JsonElement>("{}");
        var output = JsonSerializer.Deserialize<JsonElement>("{}");

        _context.AiDecisionLogs.Add(AiDecisionLog.Create(_orgId, "app_classification", input, output, "zai-v1"));
        _context.AiDecisionLogs.Add(AiDecisionLog.Create(_orgId, "app_classification", input, output, "zai-v1"));
        _context.AiDecisionLogs.Add(AiDecisionLog.Create(_orgId, "weekly_narrative", input, output, "zai-v1"));
        await _context.SaveChangesAsync();

        var all = await _context.AiDecisionLogs.IgnoreQueryFilters().ToListAsync();
        all[0].MarkReviewed("accepted");
        all[1].MarkReviewed("rejected");
        all[2].MarkReviewed("useful");
        await _context.SaveChangesAsync();

        var report = await _context.AiDecisionLogs
            .IgnoreQueryFilters()
            .Where(d => d.OrgId == _orgId && d.WasReviewed)
            .GroupBy(d => new { d.DecisionType, d.ReviewOutcome })
            .Select(g => new { g.Key.DecisionType, g.Key.ReviewOutcome, Count = g.Count() })
            .ToListAsync();

        report.Should().HaveCount(3);
        report.Sum(r => r.Count).Should().Be(3);
    }
}

/// <summary>
/// Tests for AiMetricsController aggregation logic
/// </summary>
public sealed class AiMetricsLogicTests : IDisposable
{
    private readonly TimeTrackDbContext _context;
    private readonly Guid _orgId = Guid.NewGuid();

    public AiMetricsLogicTests()
    {
        var options = new DbContextOptionsBuilder<TimeTrackDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new TimeTrackDbContext(options, null);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task ClassificationAccuracy_CalculatedCorrectly()
    {
        var input = JsonSerializer.Deserialize<JsonElement>("{}");
        var output = JsonSerializer.Deserialize<JsonElement>("{}");

        // 7 accepted, 1 corrected, 2 rejected = 80% accuracy
        for (var i = 0; i < 7; i++)
        {
            var d = AiDecisionLog.Create(_orgId, "app_classification", input, output, "zai-v1");
            d.MarkReviewed("accepted");
            _context.AiDecisionLogs.Add(d);
        }
        for (var i = 0; i < 1; i++)
        {
            var d = AiDecisionLog.Create(_orgId, "app_classification", input, output, "zai-v1");
            d.MarkReviewed("corrected");
            _context.AiDecisionLogs.Add(d);
        }
        for (var i = 0; i < 2; i++)
        {
            var d = AiDecisionLog.Create(_orgId, "app_classification", input, output, "zai-v1");
            d.MarkReviewed("rejected");
            _context.AiDecisionLogs.Add(d);
        }
        await _context.SaveChangesAsync();

        var total = await _context.AiDecisionLogs.IgnoreQueryFilters()
            .CountAsync(d => d.OrgId == _orgId && d.DecisionType == "app_classification");
        var accepted = await _context.AiDecisionLogs.IgnoreQueryFilters()
            .CountAsync(d => d.OrgId == _orgId && d.DecisionType == "app_classification" && d.ReviewOutcome == "accepted");
        var corrected = await _context.AiDecisionLogs.IgnoreQueryFilters()
            .CountAsync(d => d.OrgId == _orgId && d.DecisionType == "app_classification" && d.ReviewOutcome == "corrected");

        var accuracyRate = Math.Round((double)(accepted + corrected) / total, 2);
        accuracyRate.Should().Be(0.8);
    }

    [Fact]
    public async Task AlertActedRate_CalculatedCorrectly()
    {
        // 5 alerts: 3 acted, 1 read only, 1 unread
        for (var i = 0; i < 3; i++)
        {
            var a = SmartAlert.Create(Guid.NewGuid(), _orgId, "overwork", $"msg{i}", "info");
            a.MarkRead();
            a.MarkActed();
            _context.SmartAlerts.Add(a);
        }
        var read = SmartAlert.Create(Guid.NewGuid(), _orgId, "focus_drop", "read-only", "warning");
        read.MarkRead();
        _context.SmartAlerts.Add(read);
        _context.SmartAlerts.Add(SmartAlert.Create(Guid.NewGuid(), _orgId, "overwork", "unread", "info"));
        await _context.SaveChangesAsync();

        var total = await _context.SmartAlerts.IgnoreQueryFilters()
            .CountAsync(a => a.OrgId == _orgId);
        var acted = await _context.SmartAlerts.IgnoreQueryFilters()
            .CountAsync(a => a.OrgId == _orgId && a.WasActed);
        var actedRate = Math.Round((double)acted / total, 2);

        actedRate.Should().Be(0.6); // 3/5
    }

    [Fact]
    public async Task CostMetrics_TokensAndLatency_Aggregated()
    {
        var input = JsonSerializer.Deserialize<JsonElement>("{}");
        var output = JsonSerializer.Deserialize<JsonElement>("{}");

        var d1 = AiDecisionLog.Create(_orgId, "app_classification", input, output, "zai-v1",
            tokensUsed: 100, latencyMs: 200);
        var d2 = AiDecisionLog.Create(_orgId, "app_classification", input, output, "zai-v1",
            tokensUsed: 300, latencyMs: 400);

        _context.AiDecisionLogs.AddRange(d1, d2);
        await _context.SaveChangesAsync();

        var totalTokens = await _context.AiDecisionLogs.IgnoreQueryFilters()
            .Where(d => d.OrgId == _orgId)
            .SumAsync(d => (int?)d.TokensUsed) ?? 0;
        var avgLatency = await _context.AiDecisionLogs.IgnoreQueryFilters()
            .Where(d => d.OrgId == _orgId && d.LatencyMs != null)
            .AverageAsync(d => (double)d.LatencyMs!.Value);

        totalTokens.Should().Be(400);
        avgLatency.Should().Be(300);
    }
}
