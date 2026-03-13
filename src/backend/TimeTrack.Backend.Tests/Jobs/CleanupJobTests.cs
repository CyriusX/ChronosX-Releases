using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Jobs;
using TimeTrack.Backend.Infrastructure.Persistence;
using Xunit;

namespace TimeTrack.Backend.Tests.Jobs;

/// <summary>
/// Unit tests for CleanupJob
/// </summary>
public class CleanupJobTests : IDisposable
{
    private readonly TimeTrackDbContext _context;
    private readonly Mock<IIdempotencyKeyRepository> _idempotencyKeyRepositoryMock;
    private readonly Mock<ILogger<CleanupJob>> _loggerMock;
    private readonly CleanupJob _job;

    public CleanupJobTests()
    {
        var options = new DbContextOptionsBuilder<TimeTrackDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new TimeTrackDbContext(options, null);
        _idempotencyKeyRepositoryMock = new Mock<IIdempotencyKeyRepository>();
        _loggerMock = new Mock<ILogger<CleanupJob>>();

        _job = new CleanupJob(_context, _idempotencyKeyRepositoryMock.Object, _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task CleanupJob_WhenNoExpiredKeys_LogsCompletion()
    {
        // Arrange - no expired keys in database

        // Act
        await _job.ExecuteAsync();

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Cleanup job completed")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task CleanupJob_DeletesExpiredKeys()
    {
        // Arrange
        var orgId = Guid.NewGuid();

        // Create expired key (using reflection to set ExpiresAt since Create method sets it to 7 days from now)
        var expiredKey = IdempotencyKey.Create(
            orgId,
            "expired-key",
            "TestEntity",
            Guid.NewGuid());

        // Use reflection to modify ExpiresAt to be in the past
        expiredKey.GetType().GetProperty("ExpiresAt")?.SetValue(expiredKey, DateTime.UtcNow.AddDays(-1));

        // Create valid key
        var validKey = IdempotencyKey.Create(
            orgId,
            "valid-key",
            "TestEntity",
            Guid.NewGuid());

        _context.IdempotencyKeys.AddRange(expiredKey, validKey);
        await _context.SaveChangesAsync();

        // Act
        await _job.ExecuteAsync();

        // Assert
        var remainingKeys = await _context.IdempotencyKeys.IgnoreQueryFilters().CountAsync();
        remainingKeys.Should().Be(1); // Only valid key should remain
    }

    [Fact]
    public async Task CleanupJob_DeletesOnlyExpiredKeys()
    {
        // Arrange
        var orgId = Guid.NewGuid();

        // Create multiple expired keys
        var expiredKeys = new List<IdempotencyKey>();
        for (int i = 0; i < 5; i++)
        {
            var key = IdempotencyKey.Create(
                orgId,
                $"expired-key-{i}",
                "TestEntity",
                Guid.NewGuid());
            key.GetType().GetProperty("ExpiresAt")?.SetValue(key, DateTime.UtcNow.AddDays(-10));
            expiredKeys.Add(key);
        }

        // Create multiple valid keys
        var validKeys = new List<IdempotencyKey>();
        for (int i = 0; i < 3; i++)
        {
            var key = IdempotencyKey.Create(
                orgId,
                $"valid-key-{i}",
                "TestEntity",
                Guid.NewGuid());
            // Keep default ExpiresAt (7 days in future)
            validKeys.Add(key);
        }

        _context.IdempotencyKeys.AddRange(expiredKeys);
        _context.IdempotencyKeys.AddRange(validKeys);
        await _context.SaveChangesAsync();

        // Act
        await _job.ExecuteAsync();

        // Assert
        var remainingKeys = await _context.IdempotencyKeys.IgnoreQueryFilters().CountAsync();
        remainingKeys.Should().Be(3); // Only 3 valid keys should remain
    }

    [Fact]
    public async Task CleanupJob_WhenAllKeysExpired_DeletesAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();

        // Create multiple expired keys
        var expiredKeys = new List<IdempotencyKey>();
        for (int i = 0; i < 5; i++)
        {
            var key = IdempotencyKey.Create(
                orgId,
                $"expired-key-{i}",
                "TestEntity",
                Guid.NewGuid());
            key.GetType().GetProperty("ExpiresAt")?.SetValue(key, DateTime.UtcNow.AddDays(-10));
            expiredKeys.Add(key);
        }

        _context.IdempotencyKeys.AddRange(expiredKeys);
        await _context.SaveChangesAsync();

        // Act
        await _job.ExecuteAsync();

        // Assert
        var remainingKeys = await _context.IdempotencyKeys.IgnoreQueryFilters().CountAsync();
        remainingKeys.Should().Be(0); // All expired keys should be deleted
    }

    [Fact]
    public async Task CleanupJob_LogsCorrectNumberOfDeletedKeys()
    {
        // Arrange
        var orgId = Guid.NewGuid();

        var expiredKey1 = IdempotencyKey.Create(
            orgId,
            "expired-key-1",
            "TestEntity",
            Guid.NewGuid());
        expiredKey1.GetType().GetProperty("ExpiresAt")?.SetValue(expiredKey1, DateTime.UtcNow.AddDays(-10));

        var expiredKey2 = IdempotencyKey.Create(
            orgId,
            "expired-key-2",
            "TestEntity",
            Guid.NewGuid());
        expiredKey2.GetType().GetProperty("ExpiresAt")?.SetValue(expiredKey2, DateTime.UtcNow.AddDays(-8));

        _context.IdempotencyKeys.AddRange(expiredKey1, expiredKey2);
        await _context.SaveChangesAsync();

        // Act
        await _job.ExecuteAsync();

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Deleted 2 expired idempotency keys")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
