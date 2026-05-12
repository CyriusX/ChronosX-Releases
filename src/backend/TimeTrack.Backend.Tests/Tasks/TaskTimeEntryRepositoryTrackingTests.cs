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

namespace TimeTrack.Backend.Tests.Tasks;

public sealed class TaskTimeEntryRepositoryTrackingTests
{
    [Fact]
    public async Task UpdateAsync_ShouldNotAttachTaskGraph_WhenEntryWasLoadedNoTracking()
    {
        var options = new DbContextOptionsBuilder<TimeTrackDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new TimeTrackDbContext(options, new TestCurrentUserContext());
        var tasksRepo = new ProjectTaskRepository(db);
        var entriesRepo = new TaskTimeEntryRepository(db);

        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var task = ProjectTask.Create(orgId, projectId, "Test task", createdByUserId: userId, assignedUserId: userId);
        db.ProjectTasks.Add(task);

        var open = TaskTimeEntry.Open(orgId, task.Id, userId);
        db.TaskTimeEntries.Add(open);

        await db.SaveChangesAsync(CancellationToken.None);

        // Simulate the MoveTask handler pattern:
        // - Task is tracked in the DbContext
        // - Open entry is loaded AsNoTracking()
        // - Entry is closed and updated
        _ = await tasksRepo.GetByIdAsync(task.Id, CancellationToken.None);

        var openEntries = await entriesRepo.ListOpenForUserAsync(userId, CancellationToken.None);
        openEntries.Should().HaveCount(1);

        var entryToClose = openEntries.Single();
        entryToClose.Close(DateTime.UtcNow);

        Func<Task> act = async () => await entriesRepo.UpdateAsync(entryToClose, CancellationToken.None);
        await act.Should().NotThrowAsync();
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

