using Dapper;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Domain.Entities;
using TimeTrack.Agent.Domain.ValueObjects;
using TimeTrack.Agent.Infrastructure.Persistence;
using Xunit;

namespace TimeTrack.Agent.Tests.Infrastructure;

public sealed class IdlePeriodRepositoryTests
{
    [Fact]
    public async Task SaveWithOutboxAsync_ShouldNotThrow_WhenCalledTwiceSequentially()
    {
        await using var context = new SqliteContext(":memory:", NullLogger<SqliteContext>.Instance);
        await context.InitializeSchemaAsync();

        var repo = new IdlePeriodRepository(
            context,
            new Mock<IOutboxRepository>().Object,
            NullLogger<IdlePeriodRepository>.Instance);

        var now = DateTime.UtcNow;
        var period = CreateIdlePeriod(now);

        var outboxItem1 = OutboxItem.Create("idle_period", period.Id, "{\"ok\":true}", $"k:{period.Id}:1");
        var outboxItem2 = OutboxItem.Create("idle_period", period.Id, "{\"ok\":true}", $"k:{period.Id}:2");

        var act = async () =>
        {
            await repo.SaveWithOutboxAsync(period, new[] { outboxItem1 });
            await repo.SaveWithOutboxAsync(period, new[] { outboxItem2 });
        };

        await act.Should().NotThrowAsync();

        var connection = await context.GetConnectionAsync();
        var count = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM idle_periods WHERE id = @Id",
            new { Id = period.Id.ToString() });

        count.Should().Be(1);
    }

    private static IdlePeriod CreateIdlePeriod(DateTime now)
    {
        var userId = Guid.NewGuid();
        var timeRange = new TimeRange(now, now.AddSeconds(30));
        return new IdlePeriod(Guid.NewGuid(), userId, timeRange, thresholdSeconds: 5, isSystemDetected: true);
    }
}

