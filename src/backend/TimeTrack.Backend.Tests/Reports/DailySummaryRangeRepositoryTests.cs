using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Infrastructure.Persistence;
using TimeTrack.Backend.Infrastructure.Repositories;
using TimeTrack.Backend.Domain.Entities;
using Xunit;

namespace TimeTrack.Backend.Tests.Reports;

public sealed class DailySummaryRangeRepositoryTests
{
    [Fact]
    public async Task GetDailySummaryRangeAsync_ShouldClipCrossMidnightAndMergeOverlaps()
    {
        var options = new DbContextOptionsBuilder<TimeTrackDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new TimeTrackDbContext(options, new TestCurrentUserContext());
        var repo = new ReportRepository(db);

        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Day 1: 2026-03-17 (UTC)
        var day1 = new DateTime(2026, 03, 17, 0, 0, 0, DateTimeKind.Utc);
        var day2 = day1.AddDays(1);

        // Overlapping productive sessions: union should be 10:00-10:45 (2700s), not 3600s.
        var s1 = ActivitySession.Create(Guid.NewGuid(), orgId, deviceId, userId, "code.exe", "Code", "productive",
            day1.AddHours(10), day1.AddHours(10).AddMinutes(30), $"k-{Guid.NewGuid()}");
        var s2 = ActivitySession.Create(Guid.NewGuid(), orgId, deviceId, userId, "code.exe", "Code", "productive",
            day1.AddHours(10).AddMinutes(15), day1.AddHours(10).AddMinutes(45), $"k-{Guid.NewGuid()}");

        // Distraction session (unique app count should be 1).
        var s3 = ActivitySession.Create(Guid.NewGuid(), orgId, deviceId, userId, "twitter.exe", "Twitter", "distraction",
            day1.AddHours(11), day1.AddHours(11).AddMinutes(10), $"k-{Guid.NewGuid()}");

        // Cross-midnight productive session (10min on each day).
        var s4 = ActivitySession.Create(Guid.NewGuid(), orgId, deviceId, userId, "terminal.exe", "Terminal", "neutral",
            day1.AddHours(23).AddMinutes(50), day2.AddMinutes(10), $"k-{Guid.NewGuid()}");
        s4.LinkToTask(Guid.NewGuid(), Guid.NewGuid()); // task-linked => counts as productive in summary-range

        db.ActivitySessions.AddRange(s1, s2, s3, s4);

        // Idle period 5min on day 1.
        var idle = IdlePeriod.Create(Guid.NewGuid(), orgId, deviceId, userId,
            day1.AddHours(12), day1.AddHours(12).AddMinutes(5), $"i-{Guid.NewGuid()}");
        db.IdlePeriods.Add(idle);

        await db.SaveChangesAsync(CancellationToken.None);

        var results = (await repo.GetDailySummaryRangeAsync(
                userIds: new[] { userId },
                startDate: day1,
                endDate: day2,
                timezone: null,
                cancellationToken: CancellationToken.None))
            .OrderBy(r => r.Date)
            .ToList();

        results.Should().HaveCount(2);

        var r1 = results[0];
        r1.Date.Date.Should().Be(day1.Date);
        r1.TotalActiveSeconds.Should().Be(3900); // 2700 (union) + 600 + 600
        r1.ProductiveSeconds.Should().Be(3300);  // 2700 (union) + 600 (task-linked slice)
        r1.TotalIdleSeconds.Should().Be(300);
        r1.DistractionCount.Should().Be(1);
        r1.ProductivityRatio.Should().BeApproximately(3300d / 3900d, 1e-6);

        var r2 = results[1];
        r2.Date.Date.Should().Be(day2.Date);
        r2.TotalActiveSeconds.Should().Be(600);
        r2.ProductiveSeconds.Should().Be(600);
        r2.TotalIdleSeconds.Should().Be(0);
        r2.DistractionCount.Should().Be(0);
    }

    private sealed class TestCurrentUserContext : ICurrentUserContext
    {
        public Guid? UserId => null;
        public Guid? OrgId => null;
        public Guid? DeviceId => null;
        public TimeTrack.Backend.Domain.ValueObjects.UserRole? Role => null;
        public bool IsAuthenticated => false; // bypass multi-tenant filters in unit tests
        public bool IsPlatformAdmin => false;
        public bool IsInRole(TimeTrack.Backend.Domain.ValueObjects.UserRole role) => false;
    }
}
