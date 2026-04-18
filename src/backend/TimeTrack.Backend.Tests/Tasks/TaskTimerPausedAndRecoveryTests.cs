using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Tasks.Commands;
using TimeTrack.Backend.Application.Tasks.Queries;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.ValueObjects;
using TimeTrack.Backend.Infrastructure.Persistence;
using TimeTrack.Backend.Infrastructure.Repositories;
using Xunit;

namespace TimeTrack.Backend.Tests.Tasks;

public sealed class TaskTimerPausedAndRecoveryTests
{
    [Fact]
    public async Task ListProjectTasks_ShouldNotCountRunningSeconds_WhenOpenEntryIsPaused()
    {
        var options = new DbContextOptionsBuilder<TimeTrackDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ctx = new TestCurrentUserContext(userId, orgId, UserRole.Admin);

        await using var db = new TimeTrackDbContext(options, ctx);
        var projectsRepo = new ProjectRepository(db);
        var membersRepo = new ProjectMemberRepository(db);
        var tasksRepo = new ProjectTaskRepository(db);
        var entriesRepo = new TaskTimeEntryRepository(db);

        var project = Project.Create(orgId, userId, "Test project");
        db.Projects.Add(project);

        var task = ProjectTask.Create(orgId, project.Id, "Task", createdByUserId: userId, assignedUserId: userId, position: 1024);
        task.MoveTo(ProjectTaskStatus.InProgress, 1024);
        db.ProjectTasks.Add(task);

        var open = TaskTimeEntry.Open(orgId, task.Id, userId);
        db.TaskTimeEntries.Add(open);
        await db.SaveChangesAsync(CancellationToken.None);

        // Let the timer run briefly, then pause.
        await Task.Delay(1200);
        open.Pause();
        await db.SaveChangesAsync(CancellationToken.None);

        var handler = new ListProjectTasksQueryHandler(projectsRepo, membersRepo, tasksRepo, entriesRepo, ctx);

        var res1 = await handler.Handle(new ListProjectTasksQuery(project.Id), CancellationToken.None);
        var dto1 = res1.Tasks.Single(t => t.Id == task.Id);
        dto1.IsPaused.Should().BeTrue();
        dto1.IsRunning.Should().BeFalse();
        dto1.RunningSeconds.Should().NotBeNull();
        var running1 = dto1.RunningSeconds!.Value;

        await Task.Delay(1100);

        var res2 = await handler.Handle(new ListProjectTasksQuery(project.Id), CancellationToken.None);
        var dto2 = res2.Tasks.Single(t => t.Id == task.Id);
        dto2.IsPaused.Should().BeTrue();
        dto2.IsRunning.Should().BeFalse();
        dto2.RunningSeconds.Should().Be(running1);
    }

    [Fact]
    public async Task CloseOpenTaskTimer_ShouldCloseEntry_AccumulateTime_AndNormalizeTaskStatus()
    {
        var options = new DbContextOptionsBuilder<TimeTrackDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ctx = new TestCurrentUserContext(userId, orgId, UserRole.Admin);

        await using var db = new TimeTrackDbContext(options, ctx);
        var tasksRepo = new ProjectTaskRepository(db);
        var entriesRepo = new TaskTimeEntryRepository(db);

        var project = Project.Create(orgId, userId, "Test project");
        db.Projects.Add(project);

        var task = ProjectTask.Create(orgId, project.Id, "Task", createdByUserId: userId, assignedUserId: userId, position: 1024);
        task.MoveTo(ProjectTaskStatus.InProgress, 1024);
        db.ProjectTasks.Add(task);

        var open = TaskTimeEntry.Open(orgId, task.Id, userId);
        db.TaskTimeEntries.Add(open);
        await db.SaveChangesAsync(CancellationToken.None);

        await Task.Delay(1200);

        var handler = new CloseOpenTaskTimerCommandHandler(entriesRepo, tasksRepo, ctx);
        await handler.Handle(new CloseOpenTaskTimerCommand(), CancellationToken.None);

        var entryAfter = await db.TaskTimeEntries.FirstAsync(e => e.Id == open.Id, CancellationToken.None);
        entryAfter.EndedAt.Should().NotBeNull();

        var taskAfter = await db.ProjectTasks.FirstAsync(t => t.Id == task.Id, CancellationToken.None);
        taskAfter.Status.Should().Be(ProjectTaskStatus.Todo);
        taskAfter.TotalSecondsWorked.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ListUserTaskEntries_ShouldThrowForbidden_WhenNotManager()
    {
        var options = new DbContextOptionsBuilder<TimeTrackDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var ctx = new TestCurrentUserContext(userId, orgId, UserRole.Colaborador);

        await using var db = new TimeTrackDbContext(options, ctx);
        var entriesRepo = new TaskTimeEntryRepository(db);
        var handler = new ListUserTaskEntriesQueryHandler(entriesRepo, ctx);

        Func<Task> act = async () =>
            await handler.Handle(new ListUserTaskEntriesQuery(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow)), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    private sealed class TestCurrentUserContext : ICurrentUserContext
    {
        public TestCurrentUserContext(Guid userId, Guid orgId, UserRole role)
        {
            UserId = userId;
            OrgId = orgId;
            Role = role;
        }

        public Guid? UserId { get; }
        public Guid? OrgId { get; }
        public Guid? DeviceId => null;
        public UserRole? Role { get; }
        public bool IsAuthenticated => false; // bypass multi-tenant filters in unit tests
        public bool IsInRole(UserRole role) => Role == role;
    }
}

