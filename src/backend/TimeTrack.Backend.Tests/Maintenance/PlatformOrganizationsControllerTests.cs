using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using TimeTrack.Api.Controllers;
using TimeTrack.Backend.Application.Maintenance.DTOs;
using TimeTrack.Backend.Application.Maintenance.Queries;
using Xunit;

namespace TimeTrack.Backend.Tests.Maintenance;

public sealed class PlatformOrganizationsControllerTests
{
    [Fact]
    public async Task ListAllOrganizations_ShouldReturnOk_WhenUserIsPlatformAdmin()
    {
        var mediator = new Mock<ISender>();
        var controller = new PlatformOrganizationsController(mediator.Object);

        var expectedResponse = new ListOrganizationsResponse
        {
            Organizations = new(),
            TotalCount = 0
        };

        mediator
            .Setup(m => m.Send(It.IsAny<IRequest<ListOrganizationsResponse>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        var result = await controller.ListAllOrganizations(CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().Be(expectedResponse);
    }

    [Fact]
    public async Task ListAllOrganizations_ShouldCallMediator_WithCorrectQuery()
    {
        var mediator = new Mock<ISender>();
        var controller = new PlatformOrganizationsController(mediator.Object);

        mediator
            .Setup(m => m.Send(It.IsAny<IRequest<ListOrganizationsResponse>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ListOrganizationsResponse { Organizations = new(), TotalCount = 0 });

        await controller.ListAllOrganizations(CancellationToken.None);

        mediator.Verify(m => m.Send(It.IsAny<ListAllOrganizationsQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
