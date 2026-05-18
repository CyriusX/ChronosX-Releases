using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using TimeTrack.Agent.Application.UseCases.RecordIdlePeriod;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Domain.Entities;
using TimeTrack.Agent.Domain.Services;
using TimeTrack.Agent.Domain.ValueObjects;
using Xunit;

namespace TimeTrack.Agent.Tests.UseCases;

public class RecordIdlePeriodTests
{
    private readonly Mock<IIdlePeriodRepository> _idlePeriodRepositoryMock;
    private readonly Mock<ICurrentUserContext> _userContextMock;
    private readonly Mock<IIdempotencyKeyGenerator> _idempotencyKeyGeneratorMock;
    private readonly RecordIdlePeriodUseCase _useCase;
    private readonly Guid _testUserId;
    private readonly TimeSpan _testThreshold;
    private readonly DateTime _testStartedAt;
    private readonly DateTime _testEndedAt;

    public RecordIdlePeriodTests()
    {
        _idlePeriodRepositoryMock = new Mock<IIdlePeriodRepository>();
        _userContextMock = new Mock<ICurrentUserContext>();
        _idempotencyKeyGeneratorMock = new Mock<IIdempotencyKeyGenerator>();

        var loggerMock = new Mock<ILogger<RecordIdlePeriodUseCase>>();
        _useCase = new RecordIdlePeriodUseCase(
            _idlePeriodRepositoryMock.Object,
            _userContextMock.Object,
            _idempotencyKeyGeneratorMock.Object,
            loggerMock.Object);

        _testUserId = Guid.NewGuid();
        _testThreshold = TimeSpan.FromSeconds(180);
        _testStartedAt = DateTime.UtcNow.AddMinutes(-5);
        _testEndedAt = DateTime.UtcNow;

        _userContextMock.Setup(x => x.UserId).Returns(_testUserId);
        _idempotencyKeyGeneratorMock
            .Setup(x => x.Generate(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .Returns((string entityType, Guid entityId, DateTime startedAt) =>
                $"{entityType}_{entityId}_{startedAt:yyyy-MM-ddTHH:mm:ss}");
    }

    [Fact]
    public async Task ExecuteAsync_Should_SaveIdlePeriodWithOutbox()
    {
        // Arrange
        var request = new RecordIdlePeriodRequest
        {
            StartedAt = _testStartedAt,
            EndedAt = _testEndedAt,
            ThresholdSeconds = (int)_testThreshold.TotalSeconds,
            IsSystemDetected = true
        };

        OutboxItem? capturedOutboxItem = null;

        _idlePeriodRepositoryMock
            .Setup(x => x.SaveWithOutboxAsync(
                It.IsAny<IdlePeriod>(),
                It.IsAny<IEnumerable<OutboxItem>>(),
                It.IsAny<CancellationToken>()))
            .Callback<IdlePeriod, IEnumerable<OutboxItem>, CancellationToken>((period, items, ct) =>
            {
                capturedOutboxItem = items.FirstOrDefault();
            });

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        _idlePeriodRepositoryMock.Verify(
            x => x.SaveWithOutboxAsync(
                It.IsAny<IdlePeriod>(),
                It.IsAny<IEnumerable<OutboxItem>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.NotNull(result);
        Assert.NotNull(capturedOutboxItem);
        Assert.Equal("idle_period", capturedOutboxItem.EntityType);

        // Verify payload JSON (properties are in camelCase)
        var payloadJson = capturedOutboxItem.PayloadJson;
        Assert.NotNull(payloadJson);
        Assert.Contains("\"startedAt\"", payloadJson);
        Assert.Contains("\"endedAt\"", payloadJson);

        // Verify idempotency key
        var idempotencyKey = capturedOutboxItem.IdempotencyKey;
        Assert.NotNull(idempotencyKey);
        Assert.Contains("idle_period", idempotencyKey);
    }

    [Fact]
    public async Task ExecuteAsync_WithCreateOutboxFalse_Should_SaveIdlePeriodLocallyOnly()
    {
        // Arrange
        var explicitId = Guid.NewGuid();
        IdlePeriod? captured = null;

        _idlePeriodRepositoryMock
            .Setup(x => x.SaveAsync(It.IsAny<IdlePeriod>(), It.IsAny<CancellationToken>()))
            .Callback<IdlePeriod, CancellationToken>((p, _) => captured = p);

        var request = new RecordIdlePeriodRequest
        {
            IdlePeriodId = explicitId,
            StartedAt = _testStartedAt,
            EndedAt = _testEndedAt,
            ThresholdSeconds = (int)_testThreshold.TotalSeconds,
            IsSystemDetected = true,
            CreateOutbox = false
        };

        // Act
        var result = await _useCase.ExecuteAsync(request);

        // Assert
        _idlePeriodRepositoryMock.Verify(
            x => x.SaveAsync(It.IsAny<IdlePeriod>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _idlePeriodRepositoryMock.Verify(
            x => x.SaveWithOutboxAsync(It.IsAny<IdlePeriod>(), It.IsAny<IEnumerable<OutboxItem>>(), It.IsAny<CancellationToken>()),
            Times.Never);

        Assert.NotNull(captured);
        Assert.Equal(explicitId, captured!.Id);
        Assert.Equal(explicitId, result.IdlePeriodId);
    }
}
