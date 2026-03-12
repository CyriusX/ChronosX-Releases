using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Common.Security;
using TimeTrack.Backend.Application.Reports.DTOs;
using TimeTrack.Backend.Application.Reports.Queries;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.ValueObjects;
using TimeTrack.Backend.Infrastructure.Persistence;
using TimeTrack.Backend.Infrastructure.Repositories;
using Xunit;

namespace TimeTrack.Backend.Tests.Reports;

/// <summary>
/// Tests for Export CSV functionality using in-memory database
/// </summary>
public class ExportCsvTests : IDisposable
{
    private readonly TimeTrackDbContext _dbContext;
    private readonly ActivitySessionRepository _repository;
    private readonly ExportCsvQueryHandler _handler;
    private readonly Mock<ICurrentUserContext> _currentUserMock;
    private readonly Mock<IUserAuthorizationService> _authorizationServiceMock;

    public ExportCsvTests()
    {
        var options = new DbContextOptionsBuilder<TimeTrackDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new TimeTrackDbContext(options);
        _repository = new ActivitySessionRepository(_dbContext);
        _currentUserMock = new Mock<ICurrentUserContext>();
        _authorizationServiceMock = new Mock<IUserAuthorizationService>();
        _handler = new ExportCsvQueryHandler(_repository, _authorizationServiceMock.Object, _currentUserMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task ExportCsv_WithValidData_ReturnsCorrectCsvFormat()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        _currentUserMock.SetupGet(c => c.UserId).Returns(userId);
        _currentUserMock.SetupGet(c => c.OrgId).Returns(orgId);
        _currentUserMock.SetupGet(c => c.IsAuthenticated).Returns(true);
        _authorizationServiceMock
            .Setup(s => s.CanAccessUserData(userId))
            .Returns(true);

        // Create test sessions
        var sessions = CreateTestSessions(userId, orgId, 30);
        await _repository.AddRangeAsync(sessions);

        // Act
        var query = new ExportCsvQuery(
            StartDate: DateTime.UtcNow.AddDays(-7),
            EndDate: DateTime.UtcNow
        );

        var results = new List<ExportCsvRow>();
        var asyncEnumerable = await _handler.Handle(query, CancellationToken.None);

        await foreach (var row in asyncEnumerable)
        {
            results.Add(row);
        }

        // Assert
        results.Should().NotBeEmpty();
        results.Should().HaveCountGreaterThan(0);

        // Verify CSV format
        var firstRow = results.First();
        firstRow.Data.Should().Be(DateTime.UtcNow.AddDays(-7).Date);
        firstRow.AppDisplayName.Should().NotBeNullOrEmpty();
        firstRow.TempoTotalSegundos.Should().BeGreaterThan(0);
        firstRow.SessoesCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ExportCsv_WithUnauthorizedUser_ThrowsForbiddenException()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        _currentUserMock.SetupGet(c => c.UserId).Returns(currentUserId);
        _authorizationServiceMock
            .Setup(s => s.CanAccessUserData(targetUserId))
            .Returns(false);
        _authorizationServiceMock
            .Setup(s => s.EnsureCanAccessUserData(targetUserId))
            .Throws(new ForbiddenException("Access denied"));

        // Act & Assert
        var query = new ExportCsvQuery(
            StartDate: DateTime.UtcNow.AddDays(-7),
            EndDate: DateTime.UtcNow,
            UserId: targetUserId
        );

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async Task ExportCsv_WithInvalidDateRange_ThrowsArgumentException()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _currentUserMock.SetupGet(c => c.UserId).Returns(userId);
        _authorizationServiceMock
            .Setup(s => s.CanAccessUserData(userId))
            .Returns(true);

        // Act & Assert
        var query = new ExportCsvQuery(
            StartDate: DateTime.UtcNow,
            EndDate: DateTime.UtcNow.AddDays(-7) // End before start
        );

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _handler.Handle(query, CancellationToken.None));
    }

    private static List<ActivitySession> CreateTestSessions(Guid userId, Guid orgId, int count)
    {
        var sessions = new List<ActivitySession>();
        var random = new Random();
        var baseDate = DateTime.UtcNow.Date;

        for (int i = 0; i < count; i++)
        {
            var startedAt = baseDate.AddDays(-7 + (i % 7)).AddHours(random.Next(0, 23)).AddMinutes(random.Next(0, 59));
            var endedAt = startedAt.AddMinutes(random.Next(30, 120));

            var session = ActivitySession.Create(
                id: Guid.NewGuid(),
                orgId: orgId,
                deviceId: Guid.NewGuid(),
                userId: userId,
                processName: $"App_{i % 10}",
                windowTitle: $"Window {i}",
                appCategory: "Productivity",
                startedAt: startedAt,
                endedAt: endedAt,
                idempotencyKey: $"key-{i}-{Guid.NewGuid()}"
            );

            sessions.Add(session);
        }

        return sessions;
    }
}
