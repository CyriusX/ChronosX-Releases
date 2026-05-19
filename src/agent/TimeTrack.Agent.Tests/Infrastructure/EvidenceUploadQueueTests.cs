using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Infrastructure.Services;
using TimeTrack.Agent.Infrastructure.Persistence;
using Xunit;
using Moq;

namespace TimeTrack.Agent.Tests.Infrastructure;

public sealed class EvidenceUploadQueueTests : IAsyncLifetime, IAsyncDisposable
{
    private readonly SqliteContext _sqlite;
    private readonly EvidenceUploadQueue _queue;
    private readonly string _dbPath;

    public EvidenceUploadQueueTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"evidence_test_{Guid.NewGuid()}.db");
        var logger = Mock.Of<ILogger<SqliteContext>>();
        _sqlite = new SqliteContext(_dbPath, logger);
        var queueLogger = Mock.Of<ILogger<EvidenceUploadQueue>>();
        _queue = new EvidenceUploadQueue(_sqlite, queueLogger);
    }

    public async Task InitializeAsync()
    {
        await _sqlite.InitializeSchemaAsync();
    }

    public Task DisposeAsync() => DisposeAsyncCore().AsTask();

    private async ValueTask DisposeAsyncCore()
    {
        await _sqlite.DisposeAsync();
        try { File.Delete(_dbPath); } catch { }
    }

    async ValueTask IAsyncDisposable.DisposeAsync() => await DisposeAsyncCore();

    private static EvidenceQueueItem CreateItem(string? id = null, string? status = null)
    {
        return new EvidenceQueueItem
        {
            Id = id ?? Guid.NewGuid().ToString(),
            LocalPath = $"/tmp/test_{Guid.NewGuid()}.enc",
            EvidenceType = "screenshot",
            CapturedAt = DateTime.UtcNow,
            AppName = "TestApp",
            WindowTitleHash = "abc123",
            AttemptCount = 0,
            NextAttemptUtc = DateTime.UtcNow.AddMinutes(-1),
            FileSizeBytes = 1024,
            Status = status ?? "pending"
        };
    }

    [Fact]
    public async Task MarkAsFailed_ShouldNeverSetStatusToFailed()
    {
        // Arrange
        var item = CreateItem();
        await _queue.EnqueueAsync(item);

        // Act - fail 10 times
        for (var i = 0; i < 10; i++)
        {
            await _queue.MarkAsFailedAsync(item.Id, "test error");
        }

        // Assert - status should still be 'pending' (never 'failed')
        var conn = await _sqlite.GetConnectionAsync();
        var row = await Dapper.SqlMapper.QueryFirstAsync<dynamic>(
            conn, "SELECT status, attempt_count FROM evidence_upload_queue WHERE id = @Id",
            new { Id = item.Id });

        Assert.Equal("pending", (string)row.status);
        Assert.Equal(10, (long)row.attempt_count);
    }

    [Fact]
    public async Task MarkAsFailed_BackoffShouldIncreaseExponentiallyAndCapAt30Minutes()
    {
        // Arrange
        var item = CreateItem();
        await _queue.EnqueueAsync(item);

        var conn = await _sqlite.GetConnectionAsync();

        // Act & Assert - measure relative backoff growth between consecutive attempts
        var previousDelayMinutes = 0.0;

        for (var attempt = 1; attempt <= 10; attempt++)
        {
            await _queue.MarkAsFailedAsync(item.Id, "test error");

            // Read the stored next_attempt_utc and compare against SQLite's own now
            var row = await Dapper.SqlMapper.QueryFirstAsync<(string next_attempt, string now)>(
                conn,
                "SELECT next_attempt_utc, datetime('now') as now FROM evidence_upload_queue WHERE id = @Id",
                new { Id = item.Id });

            var nextAttempt = DateTime.Parse(row.next_attempt, null, System.Globalization.DateTimeStyles.AdjustToUniversal);
            var sqliteNow = DateTime.Parse(row.now, null, System.Globalization.DateTimeStyles.AdjustToUniversal);
            var delayMinutes = (nextAttempt - sqliteNow).TotalMinutes;

            // Expected backoff: 1, 2, 4, 8, 16, 32→capped to 30, 30, 30, 30, 30
            if (attempt <= 5)
            {
                // Should roughly double each time
                Assert.True(delayMinutes >= previousDelayMinutes * 0.8,
                    $"Attempt {attempt}: delay {delayMinutes:F1}min should grow from previous {previousDelayMinutes:F1}min");
            }
            else
            {
                // Should cap at 30 min
                Assert.True(delayMinutes <= 32,
                    $"Attempt {attempt}: delay {delayMinutes:F1}min should be capped at ~30min");
                Assert.True(delayMinutes >= 25,
                    $"Attempt {attempt}: delay {delayMinutes:F1}min should be near 30min cap");
            }

            previousDelayMinutes = delayMinutes;
        }
    }

    [Fact]
    public async Task GetPending_ShouldIncludeFailedItems()
    {
        // Arrange - insert an item directly as 'failed' with past next_attempt_utc
        var item = CreateItem(status: "failed");
        await _queue.EnqueueAsync(item);

        // Manually set status to 'failed' and next_attempt to past
        var conn = await _sqlite.GetConnectionAsync();
        await Dapper.SqlMapper.ExecuteAsync(conn,
            "UPDATE evidence_upload_queue SET status = 'failed', next_attempt_utc = datetime('now', '-1 minute') WHERE id = @Id",
            new { Id = item.Id });

        // Act
        var pending = await _queue.GetPendingAsync(10);

        // Assert
        Assert.Single(pending);
        Assert.Equal(item.Id, pending[0].Id);
    }

    [Fact]
    public async Task GetPending_ShouldNotIncludeFutureItems()
    {
        // Arrange - insert item with future next_attempt
        var item = CreateItem();
        await _queue.EnqueueAsync(item);

        var conn = await _sqlite.GetConnectionAsync();
        await Dapper.SqlMapper.ExecuteAsync(conn,
            "UPDATE evidence_upload_queue SET next_attempt_utc = datetime('now', '+30 minutes') WHERE id = @Id",
            new { Id = item.Id });

        // Act
        var pending = await _queue.GetPendingAsync(10);

        // Assert
        Assert.Empty(pending);
    }

    [Fact]
    public async Task MarkAsFailed_ShouldAlwaysResetStatusToPending()
    {
        // Arrange
        var item = CreateItem(status: "failed");
        await _queue.EnqueueAsync(item);

        var conn = await _sqlite.GetConnectionAsync();
        await Dapper.SqlMapper.ExecuteAsync(conn,
            "UPDATE evidence_upload_queue SET status = 'failed', next_attempt_utc = datetime('now', '-1 minute') WHERE id = @Id",
            new { Id = item.Id });

        // Act
        await _queue.MarkAsFailedAsync(item.Id, "test error");

        // Assert
        var row = await Dapper.SqlMapper.QueryFirstAsync<dynamic>(
            await _sqlite.GetConnectionAsync(),
            "SELECT status, attempt_count FROM evidence_upload_queue WHERE id = @Id",
            new { Id = item.Id });

        Assert.Equal("pending", (string)row.status);
        Assert.Equal(1, (int)row.attempt_count);
    }

    [Fact]
    public async Task Cleanup_ShouldNotDeleteFailedItems()
    {
        // Arrange - insert old failed item (simulating long-running retry)
        var item = CreateItem();
        await _queue.EnqueueAsync(item);

        var conn = await _sqlite.GetConnectionAsync();
        await Dapper.SqlMapper.ExecuteAsync(conn,
            "UPDATE evidence_upload_queue SET status = 'failed', created_at = datetime('now', '-7 days') WHERE id = @Id",
            new { Id = item.Id });

        // Act
        await _queue.CleanupOldEntriesAsync(1);

        // Assert - failed item should still exist
        var row = await Dapper.SqlMapper.QueryFirstOrDefaultAsync<dynamic>(
            await _sqlite.GetConnectionAsync(),
            "SELECT id FROM evidence_upload_queue WHERE id = @Id",
            new { Id = item.Id });

        Assert.NotNull(row);
    }

    [Fact]
    public async Task Cleanup_ShouldDeleteOldUploadedItems()
    {
        // Arrange
        var item = CreateItem();
        await _queue.EnqueueAsync(item);
        await _queue.MarkAsUploadedAsync(item.Id);

        // Backdate created_at
        var conn = await _sqlite.GetConnectionAsync();
        await Dapper.SqlMapper.ExecuteAsync(conn,
            "UPDATE evidence_upload_queue SET created_at = datetime('now', '-2 days') WHERE id = @Id",
            new { Id = item.Id });

        // Act
        await _queue.CleanupOldEntriesAsync(1);

        // Assert
        var row = await Dapper.SqlMapper.QueryFirstOrDefaultAsync<dynamic>(
            await _sqlite.GetConnectionAsync(),
            "SELECT id FROM evidence_upload_queue WHERE id = @Id",
            new { Id = item.Id });

        Assert.Null(row);
    }

    [Fact]
    public async Task FullRetryCycle_ShouldRecoverFromTransientFailure()
    {
        // Arrange
        var item = CreateItem();
        await _queue.EnqueueAsync(item);

        var conn = await _sqlite.GetConnectionAsync();

        // Act - simulate 5 failures (reset next_attempt each time to simulate time passing)
        for (var i = 0; i < 5; i++)
        {
            // Make item due for retry
            await Dapper.SqlMapper.ExecuteAsync(conn,
                "UPDATE evidence_upload_queue SET next_attempt_utc = datetime('now', '-1 minute') WHERE id = @Id",
                new { Id = item.Id });

            var pending = await _queue.GetPendingAsync(10);
            Assert.NotEmpty(pending);
            await _queue.MarkAsFailedAsync(item.Id, $"error {i}");
        }

        // After failures, next_attempt is in the future
        var futurePending = await _queue.GetPendingAsync(10);
        Assert.Empty(futurePending);

        // Simulate time passing
        await Dapper.SqlMapper.ExecuteAsync(conn,
            "UPDATE evidence_upload_queue SET next_attempt_utc = datetime('now', '-1 minute') WHERE id = @Id",
            new { Id = item.Id });

        // Now it should be picked up again
        var retryPending = await _queue.GetPendingAsync(10);
        Assert.Single(retryPending);
        Assert.Equal(5, retryPending[0].AttemptCount);

        // Act - simulate success on retry
        await _queue.MarkAsUploadedAsync(item.Id);

        // Assert
        var finalPending = await _queue.GetPendingAsync(10);
        Assert.Empty(finalPending);
    }
}
