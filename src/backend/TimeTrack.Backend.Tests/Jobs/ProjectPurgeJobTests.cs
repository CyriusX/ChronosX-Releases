using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Infrastructure.Jobs;
using TimeTrack.Backend.Infrastructure.Persistence;
using Xunit;

namespace TimeTrack.Backend.Tests.Jobs;

public sealed class ProjectPurgeJobTests : IDisposable
{
    private readonly TimeTrackDbContext _context;
    private readonly Mock<ILogger<ProjectPurgeJob>> _logger;
    private readonly ProjectPurgeJob _job;

    public ProjectPurgeJobTests()
    {
        var options = new DbContextOptionsBuilder<TimeTrackDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new TimeTrackDbContext(options, null);
        _logger = new Mock<ILogger<ProjectPurgeJob>>();
        _job = new ProjectPurgeJob(_context, _logger.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_Should_Purge_Projects_OlderThan30Days_And_Clear_Activity_Links()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        var project = Project.Create(orgId, userId, "To Purge");
        project.SoftDelete(userId);

        // Make it eligible for purge
        project.GetType().GetProperty("DeletedAt")?.SetValue(project, DateTime.UtcNow.AddDays(-31));

        var task = ProjectTask.Create(orgId, project.Id, "T1", createdByUserId: userId);

        var session = ActivitySession.Create(
            id: Guid.NewGuid(),
            orgId: orgId,
            deviceId: deviceId,
            userId: userId,
            processName: "app",
            windowTitle: "w",
            appCategory: "cat",
            startedAt: DateTime.UtcNow.AddMinutes(-10),
            endedAt: DateTime.UtcNow.AddMinutes(-9),
            idempotencyKey: Guid.NewGuid().ToString("N"));
        session.LinkToTask(project.Id, task.Id);

        _context.Projects.Add(project);
        _context.ProjectTasks.Add(task);
        _context.ActivitySessions.Add(session);
        await _context.SaveChangesAsync();

        await _job.ExecuteAsync();

        (await _context.Projects.IgnoreQueryFilters().CountAsync()).Should().Be(0);

        var remainingSession = await _context.ActivitySessions.IgnoreQueryFilters().FirstAsync();
        remainingSession.ProjectId.Should().BeNull();
        remainingSession.TaskId.Should().BeNull();
    }
}

