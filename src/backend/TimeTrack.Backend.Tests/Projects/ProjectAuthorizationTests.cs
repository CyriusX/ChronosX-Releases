using FluentAssertions;
using Moq;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.ProjectMembers.Queries;
using TimeTrack.Backend.Application.Projects.Queries;
using TimeTrack.Backend.Application.Tasks.Queries;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;
using Xunit;

namespace TimeTrack.Backend.Tests.Projects;

public sealed class ProjectAuthorizationTests
{
    [Fact]
    public async Task ListProjects_Should_FilterToMemberProjects_ForNonManagers_EvenWhenMineOnlyIsFalse()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var p1 = Project.Create(orgId, "P1");
        var p2 = Project.Create(orgId, "P2");

        var projectsRepo = new Mock<IProjectRepository>();
        projectsRepo.Setup(r => r.GetByOrgIdAsync(orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { p1, p2 });

        var membersRepo = new Mock<IProjectMemberRepository>();
        membersRepo.Setup(r => r.ListProjectIdsForUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { p2.Id });

        var currentUser = new Mock<ICurrentUserContext>();
        currentUser.SetupGet(c => c.OrgId).Returns(orgId);
        currentUser.SetupGet(c => c.UserId).Returns(userId);
        currentUser.SetupGet(c => c.Role).Returns(UserRole.Colaborador);

        var handler = new ListProjectsQueryHandler(projectsRepo.Object, membersRepo.Object, currentUser.Object);

        var result = await handler.Handle(new ListProjectsQuery(ActiveOnly: null, MineOnly: false), CancellationToken.None);

        result.Projects.Should().HaveCount(1);
        result.Projects[0].Id.Should().Be(p2.Id);
    }

    [Fact]
    public async Task ListProjects_Should_ReturnAllOrgProjects_ForManagers_WhenMineOnlyIsFalse()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var p1 = Project.Create(orgId, "P1");
        var p2 = Project.Create(orgId, "P2");

        var projectsRepo = new Mock<IProjectRepository>();
        projectsRepo.Setup(r => r.GetByOrgIdAsync(orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { p1, p2 });

        var membersRepo = new Mock<IProjectMemberRepository>();

        var currentUser = new Mock<ICurrentUserContext>();
        currentUser.SetupGet(c => c.OrgId).Returns(orgId);
        currentUser.SetupGet(c => c.UserId).Returns(userId);
        currentUser.SetupGet(c => c.Role).Returns(UserRole.Gestor);

        var handler = new ListProjectsQueryHandler(projectsRepo.Object, membersRepo.Object, currentUser.Object);

        var result = await handler.Handle(new ListProjectsQuery(ActiveOnly: null, MineOnly: false), CancellationToken.None);

        result.Projects.Should().HaveCount(2);
        membersRepo.Verify(r => r.ListProjectIdsForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetProject_Should_Forbid_WhenUserIsNotAMember_AndNotManager()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var project = Project.Create(orgId, "P1");

        var projectsRepo = new Mock<IProjectRepository>();
        projectsRepo.Setup(r => r.GetByIdAsync(project.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        var membersRepo = new Mock<IProjectMemberRepository>();
        membersRepo.Setup(r => r.IsMemberAsync(project.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var currentUser = new Mock<ICurrentUserContext>();
        currentUser.SetupGet(c => c.UserId).Returns(userId);
        currentUser.SetupGet(c => c.Role).Returns(UserRole.Colaborador);

        var handler = new GetProjectQueryHandler(projectsRepo.Object, membersRepo.Object, currentUser.Object);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new GetProjectQuery(project.Id), CancellationToken.None));
    }

    [Fact]
    public async Task ListProjectMembers_Should_Forbid_WhenUserIsNotAMember_AndNotManager()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = Project.Create(orgId, "P1");

        var projectsRepo = new Mock<IProjectRepository>();
        projectsRepo.Setup(r => r.GetByIdAsync(project.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        var membersRepo = new Mock<IProjectMemberRepository>();
        membersRepo.Setup(r => r.IsMemberAsync(project.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var currentUser = new Mock<ICurrentUserContext>();
        currentUser.SetupGet(c => c.UserId).Returns(userId);
        currentUser.SetupGet(c => c.Role).Returns(UserRole.Colaborador);

        var handler = new ListProjectMembersQueryHandler(membersRepo.Object, projectsRepo.Object, currentUser.Object);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new ListProjectMembersQuery(project.Id), CancellationToken.None));
    }

    [Fact]
    public async Task ListProjectTasks_Should_Forbid_WhenUserIsNotAMember_AndNotManager()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var project = Project.Create(orgId, "P1");

        var projectsRepo = new Mock<IProjectRepository>();
        projectsRepo.Setup(r => r.GetByIdAsync(project.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        var membersRepo = new Mock<IProjectMemberRepository>();
        membersRepo.Setup(r => r.IsMemberAsync(project.Id, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var tasksRepo = new Mock<IProjectTaskRepository>();
        var entriesRepo = new Mock<ITaskTimeEntryRepository>();

        var currentUser = new Mock<ICurrentUserContext>();
        currentUser.SetupGet(c => c.UserId).Returns(userId);
        currentUser.SetupGet(c => c.Role).Returns(UserRole.Colaborador);

        var handler = new ListProjectTasksQueryHandler(
            projectsRepo.Object,
            membersRepo.Object,
            tasksRepo.Object,
            entriesRepo.Object,
            currentUser.Object);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new ListProjectTasksQuery(project.Id), CancellationToken.None));
    }

    [Fact]
    public async Task GetTaskById_Should_Forbid_WhenUserIsNotAMember_AndNotManager()
    {
        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var task = ProjectTask.Create(orgId, projectId, "T1", createdByUserId: userId);

        var tasksRepo = new Mock<IProjectTaskRepository>();
        tasksRepo.Setup(r => r.GetByIdAsync(task.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        var projectsRepo = new Mock<IProjectRepository>();
        projectsRepo.Setup(r => r.GetByIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Project.Create(orgId, "P1"));

        var entriesRepo = new Mock<ITaskTimeEntryRepository>();

        var membersRepo = new Mock<IProjectMemberRepository>();
        membersRepo.Setup(r => r.IsMemberAsync(projectId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var currentUser = new Mock<ICurrentUserContext>();
        currentUser.SetupGet(c => c.UserId).Returns(userId);
        currentUser.SetupGet(c => c.Role).Returns(UserRole.Colaborador);

        var handler = new GetTaskByIdQueryHandler(
            tasksRepo.Object,
            projectsRepo.Object,
            entriesRepo.Object,
            membersRepo.Object,
            currentUser.Object);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new GetTaskByIdQuery(task.Id), CancellationToken.None));
    }
}
