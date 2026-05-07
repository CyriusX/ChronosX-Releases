using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Infrastructure.Persistence;
using TimeTrack.Backend.Infrastructure.Repositories;
using Xunit;

namespace TimeTrack.Backend.Tests.Reports;

public sealed class TopFoldersNormalizationTests
{
    [Fact]
    public async Task GetTopFoldersAsync_ShouldIncludeOnlyFinderAndFileExplorerSessions()
    {
        var options = new DbContextOptionsBuilder<TimeTrackDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new TimeTrackDbContext(options, new TestCurrentUserContext());
        var repo = new ReportRepository(db);

        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var start = new DateTime(2026, 04, 17, 0, 0, 0, DateTimeKind.Utc);

        db.ActivitySessions.AddRange(
            ActivitySession.Create(Guid.NewGuid(), orgId, deviceId, userId, "code.exe", "Code", "productive",
                start.AddMinutes(1), start.AddMinutes(2), $"k-{Guid.NewGuid()}",
                filePath: "/Users/junior/Documents/file.txt"),
            ActivitySession.Create(Guid.NewGuid(), orgId, deviceId, userId, "Finder", "Finder", "neutral",
                start.AddMinutes(1), start.AddMinutes(2), $"k-{Guid.NewGuid()}",
                filePath: "/Users/junior/Documents/"),
            ActivitySession.Create(Guid.NewGuid(), orgId, deviceId, userId, "File Explorer", "File Explorer", "neutral",
                start.AddMinutes(2), start.AddMinutes(3), $"k-{Guid.NewGuid()}",
                filePath: @"C:\Users\Junior\Desktop\notes.txt"),
            ActivitySession.Create(Guid.NewGuid(), orgId, deviceId, userId, "File Explorer", "File Explorer", "neutral",
                start.AddMinutes(3), start.AddMinutes(4), $"k-{Guid.NewGuid()}",
                filePath: @"\\server\share\dir\file.txt"),
            ActivitySession.Create(Guid.NewGuid(), orgId, deviceId, userId, "Finder", "Finder", "neutral",
                start.AddMinutes(4), start.AddMinutes(5), $"k-{Guid.NewGuid()}",
                filePath: "file:///Users/junior/Downloads/file.txt"),
            ActivitySession.Create(Guid.NewGuid(), orgId, deviceId, userId, "chrome.exe", "YouTube", "distraction",
                start.AddMinutes(6), start.AddMinutes(7), $"k-{Guid.NewGuid()}",
                filePath: "https://youtube.com/watch?v=123"),
            ActivitySession.Create(Guid.NewGuid(), orgId, deviceId, userId, "code.exe", "Relative", "neutral",
                start.AddMinutes(7), start.AddMinutes(8), $"k-{Guid.NewGuid()}",
                filePath: "relative/path/to/file.txt")
        );

        await db.SaveChangesAsync(CancellationToken.None);

        var results = (await repo.GetTopFoldersAsync(
                userIds: new[] { userId },
                startDate: start.Date,
                endDate: start.Date,
                limit: 50,
                timezone: "UTC",
                cancellationToken: CancellationToken.None))
            .Select(r => r.FolderPath)
            .ToList();

        results.Should().Contain("/Users/junior/Documents");
        results.Should().Contain(@"C:\Users\Junior\Desktop");
        results.Should().Contain(@"\\server\share\dir");
        results.Should().Contain("/Users/junior/Downloads");

        results.Should().NotContain("https://youtube.com/watch?v=123");
        results.Should().NotContain("relative/path/to/file.txt");
        results.Should().NotContain("/Users/junior/Documents/file.txt");
    }

    private sealed class TestCurrentUserContext : ICurrentUserContext
    {
        public Guid? UserId => null;
        public Guid? OrgId => null;
        public Guid? DeviceId => null;
        public TimeTrack.Backend.Domain.ValueObjects.UserRole? Role => null;
        public bool IsAuthenticated => false; // bypass org query filters in unit tests
        public bool IsInRole(TimeTrack.Backend.Domain.ValueObjects.UserRole role) => false;
    }
}
