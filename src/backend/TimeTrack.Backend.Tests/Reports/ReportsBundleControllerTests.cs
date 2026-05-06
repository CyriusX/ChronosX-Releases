using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TimeTrack.Api.Controllers;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Common.Security;
using TimeTrack.Backend.Application.Reports.DTOs;
using TimeTrack.Backend.Application.Reports.Queries;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;
using Xunit;

namespace TimeTrack.Backend.Tests.Reports;

public sealed class ReportsBundleControllerTests
{
    [Fact]
    public async Task GetReportsBundle_ShouldNotRunMediatorQueriesConcurrently()
    {
        var userId = Guid.NewGuid();

        var currentUser = new Mock<ICurrentUserContext>();
        currentUser.SetupGet(c => c.UserId).Returns(userId);
        currentUser.SetupGet(c => c.Role).Returns(UserRole.Colaborador);
        currentUser.SetupGet(c => c.OrgId).Returns(Guid.NewGuid());
        currentUser.SetupGet(c => c.IsAuthenticated).Returns(true);

        var authorization = new Mock<IUserAuthorizationService>();
        authorization.Setup(a => a.EnsureCanAccessUserData(It.IsAny<Guid>()));

        var audit = new Mock<IAuditLogService>();
        var userRepo = new Mock<IUserRepository>();
        var logger = new Mock<ILogger<ReportsController>>();

        var sender = new NonConcurrentSender();

        var controller = new ReportsController(
            sender,
            authorization.Object,
            currentUser.Object,
            audit.Object,
            userRepo.Object,
            logger.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var result = await controller.GetReportsBundle(
            userId: null,
            startDate: new DateTime(2026, 03, 17),
            endDate: new DateTime(2026, 04, 16),
            groupBy: "day",
            topAppsLimit: 20,
            topPathsLimit: 20,
            topFoldersLimit: 20,
            timezone: "America/Toronto",
            allTeam: true,
            cancellationToken: CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<ReportsBundleResponse>();
    }

    private sealed class NonConcurrentSender : ISender
    {
        private int _active;

        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _active) > 1)
                throw new InvalidOperationException("Concurrent mediator Send detected");

            try
            {
                // Ensure that if the controller starts these in parallel, overlap will happen.
                await Task.Delay(10, cancellationToken);

                object response = request switch
                {
                    DailySummaryRangeQuery => new DailySummaryRangeResponse(),
                    ProductivityTrendQuery => new ProductivityTrendResponse(),
                    TopAppsQuery => new TopAppsResponse(),
                    TopPathsQuery => new TopPathsResponse(),
                    DistractionStatsQuery => new DistractionStatsResponse(),
                    CategoryDistributionQuery => new CategoryDistributionResponse(),
                    TopFoldersQuery => new TopFoldersResponse(),
                    _ => throw new NotSupportedException($"Unexpected request type: {request.GetType().Name}")
                };

                return (TResponse)response;
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
            => throw new NotSupportedException("Not used in these tests");

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not used in these tests");

        public Task Publish(object notification, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not used in these tests");

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => throw new NotSupportedException("Not used in these tests");

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not used in these tests");

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not used in these tests");
    }
}
