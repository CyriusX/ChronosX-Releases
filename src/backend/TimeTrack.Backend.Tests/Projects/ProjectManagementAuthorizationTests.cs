using FluentAssertions;
using Moq;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Projects.Commands;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;
using Xunit;

namespace TimeTrack.Backend.Tests.Projects;

public sealed class ProjectManagementAuthorizationTests
{
    [Fact]
    public async Task ArchiveProject_Should_Allow_Admin_ForAnyProject()
    {
        var orgId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var project = Project.Create(orgId, creatorId, "P1");

        var projectsRepo = new Mock<IProjectRepository>();
        projectsRepo.Setup(r => r.GetByIdAsync(project.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        projectsRepo.Setup(r => r.UpdateAsync(project, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var currentUser = new Mock<ICurrentUserContext>();
        currentUser.SetupGet(c => c.UserId).Returns(adminId);
        currentUser.Setup(c => c.IsInRole(UserRole.Admin)).Returns(true);

        var handler = new ArchiveProjectCommandHandler(projectsRepo.Object, currentUser.Object);

        await handler.Handle(new ArchiveProjectCommand(project.Id), CancellationToken.None);

        project.Status.Should().Be(ProjectStatus.Archived);
    }

    [Fact]
    public async Task ArchiveProject_Should_Allow_Creator_WhenNotAdmin()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = Project.Create(orgId, userId, "P1");

        var projectsRepo = new Mock<IProjectRepository>();
        projectsRepo.Setup(r => r.GetByIdAsync(project.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        projectsRepo.Setup(r => r.UpdateAsync(project, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var currentUser = new Mock<ICurrentUserContext>();
        currentUser.SetupGet(c => c.UserId).Returns(userId);
        currentUser.Setup(c => c.IsInRole(UserRole.Admin)).Returns(false);

        var handler = new ArchiveProjectCommandHandler(projectsRepo.Object, currentUser.Object);

        await handler.Handle(new ArchiveProjectCommand(project.Id), CancellationToken.None);

        project.Status.Should().Be(ProjectStatus.Archived);
    }

    [Fact]
    public async Task ArchiveProject_Should_Forbid_WhenNotCreator_AndNotAdmin()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var project = Project.Create(orgId, creatorId, "P1");

        var projectsRepo = new Mock<IProjectRepository>();
        projectsRepo.Setup(r => r.GetByIdAsync(project.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        var currentUser = new Mock<ICurrentUserContext>();
        currentUser.SetupGet(c => c.UserId).Returns(userId);
        currentUser.Setup(c => c.IsInRole(UserRole.Admin)).Returns(false);

        var handler = new ArchiveProjectCommandHandler(projectsRepo.Object, currentUser.Object);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new ArchiveProjectCommand(project.Id), CancellationToken.None));
    }

    [Fact]
    public async Task DeleteProject_Should_SoftDelete_Project_And_SoftDelete_Tasks()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = Project.Create(orgId, userId, "P1");

        var projectsRepo = new Mock<IProjectRepository>();
        projectsRepo.Setup(r => r.GetByIdAsync(project.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);
        projectsRepo.Setup(r => r.UpdateAsync(project, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var tasksRepo = new Mock<IProjectTaskRepository>();
        tasksRepo.Setup(r => r.SoftDeleteByProjectAsync(project.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var currentUser = new Mock<ICurrentUserContext>();
        currentUser.SetupGet(c => c.UserId).Returns(userId);
        currentUser.Setup(c => c.IsInRole(UserRole.Admin)).Returns(false);

        var handler = new DeleteProjectCommandHandler(projectsRepo.Object, tasksRepo.Object, currentUser.Object);

        await handler.Handle(new DeleteProjectCommand(project.Id), CancellationToken.None);

        project.DeletedAt.Should().NotBeNull();
        project.DeletedByUserId.Should().Be(userId);
        tasksRepo.Verify(r => r.SoftDeleteByProjectAsync(project.Id, It.IsAny<CancellationToken>()), Times.Once);
    }
}

