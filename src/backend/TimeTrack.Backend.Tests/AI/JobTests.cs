using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TimeTrack.Backend.AI.Configuration;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Infrastructure.Jobs;
using TimeTrack.Backend.Infrastructure.Persistence;
using Xunit;

namespace TimeTrack.Backend.Tests.AI;

public sealed class AlertGenerationJobTests : IDisposable
{
    private readonly TimeTrackDbContext _context;
    private readonly Mock<ILogger<AlertGenerationJob>> _loggerMock;
    private readonly IOptions<AlertGenerationOptions> _options;

    public AlertGenerationJobTests()
    {
        var options = new DbContextOptionsBuilder<TimeTrackDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new TimeTrackDbContext(options, null);
        _loggerMock = new Mock<ILogger<AlertGenerationJob>>();
        _options = Options.Create(new AlertGenerationOptions());
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task ExecuteAsync_WithOverwork_CreatesAlert()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

        var score = CreateDailyFocusScore(userId, orgId, yesterday,
            productiveSeconds: 14400, distractionSeconds: 0, neutralSeconds: 0,
            focusScore: 80, contextSwitchesCount: 5);

        _context.DailyFocusScores.Add(score);
        await _context.SaveChangesAsync();

        var job = new AlertGenerationJob(_context, null, _loggerMock.Object, _options);
        await job.ExecuteAsync();

        var alerts = await _context.SmartAlerts.IgnoreQueryFilters().ToListAsync();
        alerts.Should().ContainSingle(a => a.UserId == userId && a.AlertType == "overwork");
    }

    [Fact]
    public async Task ExecuteAsync_BelowOverworkThreshold_NoOverworkAlert()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

        var score = CreateDailyFocusScore(userId, orgId, yesterday,
            productiveSeconds: 3600, distractionSeconds: 0, neutralSeconds: 0,
            focusScore: 80, contextSwitchesCount: 5);

        _context.DailyFocusScores.Add(score);
        await _context.SaveChangesAsync();

        var job = new AlertGenerationJob(_context, null, _loggerMock.Object, _options);
        await job.ExecuteAsync();

        var alerts = await _context.SmartAlerts.IgnoreQueryFilters().ToListAsync();
        alerts.Should().NotContain(a => a.AlertType == "overwork");
    }

    [Fact]
    public async Task ExecuteAsync_WithFocusDrop_CreatesAlert()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        var thirtyDaysAgo = yesterday.AddDays(-30);

        // Create 14 days of baseline with focus score 80
        for (var i = 0; i < 14; i++)
        {
            var date = thirtyDaysAgo.AddDays(i);
            var baselineScore = CreateDailyFocusScore(userId, orgId, date,
                productiveSeconds: 28800, distractionSeconds: 0, neutralSeconds: 0,
                focusScore: 80, contextSwitchesCount: 5);
            _context.DailyFocusScores.Add(baselineScore);
        }

        // Yesterday: focus score well below 60% of 80 (threshold = 48)
        var dropScore = CreateDailyFocusScore(userId, orgId, yesterday,
            productiveSeconds: 28800, distractionSeconds: 0, neutralSeconds: 0,
            focusScore: 20, contextSwitchesCount: 5);
        _context.DailyFocusScores.Add(dropScore);
        await _context.SaveChangesAsync();

        var job = new AlertGenerationJob(_context, null, _loggerMock.Object, _options);
        await job.ExecuteAsync();

        var alerts = await _context.SmartAlerts.IgnoreQueryFilters().ToListAsync();
        alerts.Should().ContainSingle(a => a.UserId == userId && a.AlertType == "focus_drop");
    }

    [Fact]
    public async Task ExecuteAsync_DuplicateAlert_NotCreated()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);

        var score = CreateDailyFocusScore(userId, orgId, yesterday,
            productiveSeconds: 14400, distractionSeconds: 0, neutralSeconds: 0,
            focusScore: 80, contextSwitchesCount: 5);

        var existingAlert = SmartAlert.Create(userId, orgId, "overwork", "msg", "info");

        // Set CreatedAt to yesterday so the duplicate check matches
        var createdAtProp = typeof(SmartAlert).GetProperty("CreatedAt",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        createdAtProp!.SetValue(existingAlert, yesterday.ToDateTime(TimeOnly.MinValue));

        _context.DailyFocusScores.Add(score);
        _context.SmartAlerts.Add(existingAlert);
        await _context.SaveChangesAsync();

        var job = new AlertGenerationJob(_context, null, _loggerMock.Object, _options);
        await job.ExecuteAsync();

        var alerts = await _context.SmartAlerts.IgnoreQueryFilters()
            .Where(a => a.AlertType == "overwork" && a.UserId == userId)
            .ToListAsync();
        alerts.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithProductivityDropAnomaly_CreatesManagerAlert()
    {
        var orgId = Guid.NewGuid();
        var memberUserId = Guid.NewGuid();
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
        var evidence = JsonSerializer.Deserialize<JsonElement>("{\"baselineAvg\": 0.7}");

        _context.BehavioralAnomalies.Add(BehavioralAnomaly.Create(
            memberUserId, orgId, "productivity_drop", "warning", yesterday,
            evidence, 0.7, 0.3));
        await _context.SaveChangesAsync();

        var job = new AlertGenerationJob(_context, null, _loggerMock.Object, _options);
        await job.ExecuteAsync();

        var alerts = await _context.SmartAlerts.IgnoreQueryFilters().ToListAsync();
        alerts.Should().ContainSingle(a => a.AlertType == "team_productivity_drop"
            && a.AboutUserId == memberUserId);
    }

    private static DailyFocusScore CreateDailyFocusScore(
        Guid userId, Guid orgId, DateOnly date,
        int productiveSeconds, int distractionSeconds, int neutralSeconds,
        short focusScore, int contextSwitchesCount)
    {
        var totalMs = (productiveSeconds + distractionSeconds + neutralSeconds) * 1000L;
        var score = DailyFocusScore.Create(
            Guid.NewGuid(), orgId, userId, date,
            totalMs, productiveSeconds * 1000L, distractionSeconds * 1000L,
            0, 0, 0, 0, focusScore);

        score.UpdateFeatures(
            productiveSeconds, distractionSeconds, neutralSeconds,
            contextSwitchesCount, 0, null, 0, 0, 0,
            0, 0, 0, 0);

        return score;
    }
}

public sealed class PatternDetectionJobTests : IDisposable
{
    private readonly TimeTrackDbContext _context;
    private readonly Mock<ILogger<PatternDetectionJob>> _loggerMock;
    private readonly IOptions<PatternDetectionOptions> _options;

    public PatternDetectionJobTests()
    {
        var options = new DbContextOptionsBuilder<TimeTrackDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new TimeTrackDbContext(options, null);
        _loggerMock = new Mock<ILogger<PatternDetectionJob>>();
        _options = Options.Create(new PatternDetectionOptions());
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task ExecuteAsync_WithMorningProductiveUser_DetectsPattern()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var fourteenDaysAgo = today.AddDays(-14);

        // Create scores for enough weekdays to guarantee >= 4 work days
        for (var i = 0; i < 14; i++)
        {
            var date = fourteenDaysAgo.AddDays(i);
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            if (date < today.AddDays(-7)) continue; // only last 7 days
            var score = CreateDailyFocusScore(userId, orgId, date,
                productiveSeconds: 28800, distractionSeconds: 0, neutralSeconds: 0,
                focusScore: 80, contextSwitchesCount: 5);
            score.UpdateFeatures(28800, 0, 0, 5, 0, null, 0, 0, 0, 0, 0, 0, 0);
            _context.DailyFocusScores.Add(score);
        }
        await _context.SaveChangesAsync();

        var job = new PatternDetectionJob(_context, null, _loggerMock.Object, _options);
        await job.ExecuteAsync();

        var patterns = await _context.UserPatterns.IgnoreQueryFilters().ToListAsync();
        patterns.Should().Contain(p => p.UserId == userId && p.PatternTag == "morning_productive" && p.IsActive);
    }

    [Fact]
    public async Task ExecuteAsync_WithHighContextSwitching_DetectsPattern()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var sevenDaysAgo = today.AddDays(-7);

        for (var i = 0; i < 4; i++)
        {
            var score = CreateDailyFocusScore(userId, orgId, sevenDaysAgo.AddDays(i),
                productiveSeconds: 28800, distractionSeconds: 0, neutralSeconds: 0,
                focusScore: 50, contextSwitchesCount: 50);
            score.UpdateFeatures(28800, 0, 0, 50, 0, null, 0, 0, 0, 0, 0, 0, 0);
            _context.DailyFocusScores.Add(score);
        }
        await _context.SaveChangesAsync();

        var job = new PatternDetectionJob(_context, null, _loggerMock.Object, _options);
        await job.ExecuteAsync();

        var patterns = await _context.UserPatterns.IgnoreQueryFilters().ToListAsync();
        patterns.Should().Contain(p => p.UserId == userId && p.PatternTag == "high_context_switching");
    }

    [Fact]
    public async Task ExecuteAsync_WithDeepWorkCapable_DetectsPattern()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var sevenDaysAgo = today.AddDays(-7);

        for (var i = 0; i < 3; i++)
        {
            var score = CreateDailyFocusScore(userId, orgId, sevenDaysAgo.AddDays(i),
                productiveSeconds: 28800, distractionSeconds: 0, neutralSeconds: 0,
                focusScore: 85, contextSwitchesCount: 5);
            score.UpdateFeatures(28800, 0, 0, 5, 0, null, 0, 0, 0, 0, 0, 6000, 3000);
            _context.DailyFocusScores.Add(score);
        }
        await _context.SaveChangesAsync();

        var job = new PatternDetectionJob(_context, null, _loggerMock.Object, _options);
        await job.ExecuteAsync();

        var patterns = await _context.UserPatterns.IgnoreQueryFilters().ToListAsync();
        patterns.Should().Contain(p => p.UserId == userId && p.PatternTag == "deep_work_capable");
    }

    [Fact]
    public async Task ExecuteAsync_StalePatterns_Deactivates()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var staleDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-31);
        var evidence = JsonSerializer.Deserialize<JsonElement>("{}");

        var stalePattern = UserPattern.Create(userId, orgId, "morning_productive", staleDate, 0.8, evidence);
        _context.UserPatterns.Add(stalePattern);
        await _context.SaveChangesAsync();

        var job = new PatternDetectionJob(_context, null, _loggerMock.Object, _options);
        await job.ExecuteAsync();

        var pattern = await _context.UserPatterns.IgnoreQueryFilters().FirstAsync();
        pattern.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_InsufficientData_NoPatterns()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var sevenDaysAgo = today.AddDays(-7);

        for (var i = 0; i < 2; i++)
        {
            var score = CreateDailyFocusScore(userId, orgId, sevenDaysAgo.AddDays(i),
                productiveSeconds: 28800, distractionSeconds: 0, neutralSeconds: 0,
                focusScore: 80, contextSwitchesCount: 5);
            score.UpdateFeatures(28800, 0, 0, 5, 0, null, 0, 0, 0, 0, 0, 0, 0);
            _context.DailyFocusScores.Add(score);
        }
        await _context.SaveChangesAsync();

        var job = new PatternDetectionJob(_context, null, _loggerMock.Object, _options);
        await job.ExecuteAsync();

        var patterns = await _context.UserPatterns.IgnoreQueryFilters().ToListAsync();
        patterns.Should().BeEmpty();
    }

    private static DailyFocusScore CreateDailyFocusScore(
        Guid userId, Guid orgId, DateOnly date,
        int productiveSeconds, int distractionSeconds, int neutralSeconds,
        short focusScore, int contextSwitchesCount)
    {
        var totalMs = (productiveSeconds + distractionSeconds + neutralSeconds) * 1000L;
        var score = DailyFocusScore.Create(
            Guid.NewGuid(), orgId, userId, date,
            totalMs, productiveSeconds * 1000L, distractionSeconds * 1000L,
            0, 0, 0, 0, focusScore);

        return score;
    }
}

public sealed class AnomalyDetectionJobTests : IDisposable
{
    private readonly TimeTrackDbContext _context;
    private readonly Mock<ILogger<AnomalyDetectionJob>> _loggerMock;
    private readonly IOptions<AnomalyDetectionOptions> _options;

    public AnomalyDetectionJobTests()
    {
        var options = new DbContextOptionsBuilder<TimeTrackDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new TimeTrackDbContext(options, null);
        _loggerMock = new Mock<ILogger<AnomalyDetectionJob>>();
        _options = Options.Create(new AnomalyDetectionOptions());
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task ExecuteAsync_WithProductivityDrop_CreatesAnomaly()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var thirtyDaysAgo = today.AddDays(-30);

        // 14 days of baseline with high productivity
        for (var i = 0; i < 14; i++)
        {
            var score = CreateDailyFocusScore(userId, orgId, thirtyDaysAgo.AddDays(i),
                productiveSeconds: 25000, distractionSeconds: 2000, neutralSeconds: 1800,
                focusScore: 80, contextSwitchesCount: 5);
            _context.DailyFocusScores.Add(score);
        }

        // Yesterday: productivity drop well below mean - 2*stddev
        var yesterday = today.AddDays(-1);
        var dropScore = CreateDailyFocusScore(userId, orgId, yesterday,
            productiveSeconds: 2000, distractionSeconds: 25000, neutralSeconds: 1800,
            focusScore: 20, contextSwitchesCount: 50);
        _context.DailyFocusScores.Add(dropScore);
        await _context.SaveChangesAsync();

        var job = new AnomalyDetectionJob(_context, _loggerMock.Object, _options);
        await job.ExecuteAsync();

        var anomalies = await _context.BehavioralAnomalies.IgnoreQueryFilters().ToListAsync();
        anomalies.Should().Contain(a => a.UserId == userId && a.AnomalyType == "productivity_drop");
    }

    [Fact]
    public async Task ExecuteAsync_AbsentWorkday_CreatesAnomaly()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var thirtyDaysAgo = today.AddDays(-30);

        for (var i = 0; i < 14; i++)
        {
            var score = CreateDailyFocusScore(userId, orgId, thirtyDaysAgo.AddDays(i),
                productiveSeconds: 28800, distractionSeconds: 0, neutralSeconds: 0,
                focusScore: 80, contextSwitchesCount: 5);
            _context.DailyFocusScores.Add(score);
        }

        // Yesterday (week day) with < 1h
        var yesterday = today.AddDays(-1);
        var absentScore = CreateDailyFocusScore(userId, orgId, yesterday,
            productiveSeconds: 600, distractionSeconds: 0, neutralSeconds: 0,
            focusScore: 10, contextSwitchesCount: 0);
        _context.DailyFocusScores.Add(absentScore);
        await _context.SaveChangesAsync();

        var job = new AnomalyDetectionJob(_context, _loggerMock.Object, _options);
        await job.ExecuteAsync();

        var anomalies = await _context.BehavioralAnomalies.IgnoreQueryFilters().ToListAsync();
        anomalies.Should().Contain(a => a.UserId == userId && a.AnomalyType == "absent_workday");
    }

    [Fact]
    public async Task ExecuteAsync_InsufficientBaseline_NoAnomalies()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var yesterday = today.AddDays(-1);

        // Only 5 days of data — below 14 day minimum
        for (var i = 0; i < 5; i++)
        {
            var score = CreateDailyFocusScore(userId, orgId, yesterday.AddDays(-i),
                productiveSeconds: 28800, distractionSeconds: 0, neutralSeconds: 0,
                focusScore: 80, contextSwitchesCount: 5);
            _context.DailyFocusScores.Add(score);
        }
        await _context.SaveChangesAsync();

        var job = new AnomalyDetectionJob(_context, _loggerMock.Object, _options);
        await job.ExecuteAsync();

        var anomalies = await _context.BehavioralAnomalies.IgnoreQueryFilters().ToListAsync();
        anomalies.Should().BeEmpty();
    }

    private static DailyFocusScore CreateDailyFocusScore(
        Guid userId, Guid orgId, DateOnly date,
        int productiveSeconds, int distractionSeconds, int neutralSeconds,
        short focusScore, int contextSwitchesCount)
    {
        var totalMs = (productiveSeconds + distractionSeconds + neutralSeconds) * 1000L;
        var score = DailyFocusScore.Create(
            Guid.NewGuid(), orgId, userId, date,
            totalMs, productiveSeconds * 1000L, distractionSeconds * 1000L,
            0, 0, 0, 0, focusScore);

        score.UpdateFeatures(productiveSeconds, distractionSeconds, neutralSeconds,
            contextSwitchesCount, 0, null, 0, 0, 0, 0, 0, 0, 0);

        return score;
    }
}

public sealed class ThresholdUpdateJobTests : IDisposable
{
    private readonly TimeTrackDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly Mock<ILogger<ThresholdUpdateJob>> _loggerMock;

    public ThresholdUpdateJobTests()
    {
        var options = new DbContextOptionsBuilder<TimeTrackDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new TimeTrackDbContext(options, null);
        _cache = new MemoryCache(new MemoryCacheOptions());
        _loggerMock = new Mock<ILogger<ThresholdUpdateJob>>();
    }

    public void Dispose()
    {
        _context.Dispose();
        _cache.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_ComputesThresholdAndStoresInCache()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var thirtyDaysAgo = today.AddDays(-30);

        for (var i = 0; i < 15; i++)
        {
            var score = CreateDailyFocusScore(userId, orgId, thirtyDaysAgo.AddDays(i),
                productiveSeconds: 25000, distractionSeconds: 2000, neutralSeconds: 1800,
                focusScore: 75, contextSwitchesCount: 10);
            _context.DailyFocusScores.Add(score);
        }
        await _context.SaveChangesAsync();

        var job = new ThresholdUpdateJob(_context, _cache, _loggerMock.Object);
        await job.ExecuteAsync();

        var cached = _cache.Get<UserThreshold>(ThresholdUpdateJob.ThresholdCacheKey(userId));
        cached.Should().NotBeNull();
        cached!.AvgFocusScore.Should().Be(75);
        cached.FocusDropThreshold.Should().Be(45); // 75 * 0.6
        cached.AvgProductiveRatio.Should().BeGreaterThan(0);
        cached.AvgContextSwitches.Should().Be(10);
    }

    [Fact]
    public async Task ExecuteAsync_InsufficientData_DoesNotCache()
    {
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var thirtyDaysAgo = today.AddDays(-30);

        for (var i = 0; i < 5; i++)
        {
            var score = CreateDailyFocusScore(userId, orgId, thirtyDaysAgo.AddDays(i),
                productiveSeconds: 28800, distractionSeconds: 0, neutralSeconds: 0,
                focusScore: 80, contextSwitchesCount: 5);
            _context.DailyFocusScores.Add(score);
        }
        await _context.SaveChangesAsync();

        var job = new ThresholdUpdateJob(_context, _cache, _loggerMock.Object);
        await job.ExecuteAsync();

        var cached = _cache.Get<UserThreshold>(ThresholdUpdateJob.ThresholdCacheKey(userId));
        cached.Should().BeNull();
    }

    private static DailyFocusScore CreateDailyFocusScore(
        Guid userId, Guid orgId, DateOnly date,
        int productiveSeconds, int distractionSeconds, int neutralSeconds,
        short focusScore, int contextSwitchesCount)
    {
        var totalMs = (productiveSeconds + distractionSeconds + neutralSeconds) * 1000L;
        var score = DailyFocusScore.Create(
            Guid.NewGuid(), orgId, userId, date,
            totalMs, productiveSeconds * 1000L, distractionSeconds * 1000L,
            0, 0, 0, 0, focusScore);

        score.UpdateFeatures(productiveSeconds, distractionSeconds, neutralSeconds,
            contextSwitchesCount, 0, null, 0, 0, 0, 0, 0, 0, 0);

        return score;
    }
}
