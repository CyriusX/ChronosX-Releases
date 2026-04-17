using FluentAssertions;
using Moq;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Projects.Commands;
using TimeTrack.Backend.Application.Tasks.Commands;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;
using Xunit;

namespace TimeTrack.Backend.Tests.Projects;

public sealed class ProjectMembershipInvariantTests
{
    [Fact]
    public async Task CreateProject_Should_AddCreatorAsOwnerMember()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        Project? createdProject = null;
        ProjectMember? createdMember = null;

        var projectsRepo = new Mock<IProjectRepository>();
        projectsRepo.Setup(r => r.NameExistsInOrgAsync(It.IsAny<string>(), orgId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        projectsRepo.Setup(r => r.AddAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .Callback<Project, CancellationToken>((p, _) => createdProject = p)
            .Returns(Task.CompletedTask);

        var membersRepo = new Mock<IProjectMemberRepository>();
        membersRepo.Setup(r => r.AddAsync(It.IsAny<ProjectMember>(), It.IsAny<CancellationToken>()))
            .Callback<ProjectMember, CancellationToken>((m, _) => createdMember = m)
            .Returns(Task.CompletedTask);

        var currentUser = new Mock<ICurrentUserContext>();
        currentUser.SetupGet(c => c.OrgId).Returns(orgId);
        currentUser.SetupGet(c => c.UserId).Returns(userId);

        var handler = new CreateProjectCommandHandler(projectsRepo.Object, membersRepo.Object, currentUser.Object);

        var result = await handler.Handle(new CreateProjectCommand("My Project"), CancellationToken.None);

        createdProject.Should().NotBeNull();
        createdMember.Should().NotBeNull();
        createdMember!.UserId.Should().Be(userId);
        createdMember.ProjectId.Should().Be(createdProject!.Id);
        createdMember.Role.Should().Be(ProjectMemberRole.Owner);
        result.Id.Should().Be(createdProject.Id);
    }

    [Fact]
    public async Task CreateTask_Should_AutoAddSelfMembership_WhenMissing()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = Project.Create(orgId, userId, "P1");

        var projectsRepo = new Mock<IProjectRepository>();
        projectsRepo.Setup(r => r.GetByIdAsync(project.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        var membersRepo = new Mock<IProjectMemberRepository>();
        membersRepo.Setup(r => r.IsMemberAsync(project.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        ProjectMember? addedMember = null;
        membersRepo.Setup(r => r.AddAsync(It.IsAny<ProjectMember>(), It.IsAny<CancellationToken>()))
            .Callback<ProjectMember, CancellationToken>((m, _) => addedMember = m)
            .Returns(Task.CompletedTask);

        var tasksRepo = new Mock<IProjectTaskRepository>();
        tasksRepo.Setup(r => r.GetMaxPositionInColumnAsync(project.Id, ProjectTaskStatus.Todo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        tasksRepo.Setup(r => r.AddAsync(It.IsAny<ProjectTask>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var usersRepo = new Mock<IUserRepository>();

        var currentUser = new Mock<ICurrentUserContext>();
        currentUser.SetupGet(c => c.OrgId).Returns(orgId);
        currentUser.SetupGet(c => c.UserId).Returns(userId);
        currentUser.SetupGet(c => c.Role).Returns(UserRole.Gestor);

        var notifications = new Mock<TimeTrack.Backend.Application.Notifications.INotificationDispatcher>();

        var handler = new CreateTaskCommandHandler(
            projectsRepo.Object,
            membersRepo.Object,
            tasksRepo.Object,
            usersRepo.Object,
            currentUser.Object,
            notifications.Object);

        await handler.Handle(new CreateTaskCommand(project.Id, "T1", null, null, "medium", null), CancellationToken.None);

        addedMember.Should().NotBeNull();
        addedMember!.UserId.Should().Be(userId);
        addedMember.ProjectId.Should().Be(project.Id);
        addedMember.Role.Should().Be(ProjectMemberRole.Member);
    }
}
