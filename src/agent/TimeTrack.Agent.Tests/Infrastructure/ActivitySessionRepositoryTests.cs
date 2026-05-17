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

public sealed class ActivitySessionRepositoryTests
{
    [Fact]
    public async Task SaveWithOutboxAsync_ShouldNotThrow_WhenCalledTwiceSequentially()
    {
        await using var context = new SqliteContext(":memory:", NullLogger<SqliteContext>.Instance);
        await context.InitializeSchemaAsync();

        var repo = new ActivitySessionRepository(
            context,
            new Mock<IOutboxRepository>().Object,
            NullLogger<ActivitySessionRepository>.Instance);

        var now = DateTime.UtcNow;
        var session = CreateSession(now);

        var outboxItem1 = OutboxItem.Create("activity_session", session.Id, "{\"ok\":true}", $"k:{session.Id}:1");
        var outboxItem2 = OutboxItem.Create("activity_session", session.Id, "{\"ok\":true}", $"k:{session.Id}:2");

        var act = async () =>
        {
            await repo.SaveWithOutboxAsync(session, new[] { outboxItem1 });
            await repo.SaveWithOutboxAsync(session, new[] { outboxItem2 });
        };

        await act.Should().NotThrowAsync();

        var connection = await context.GetConnectionAsync();
        var sessionCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM activity_sessions WHERE id = @Id",
            new { Id = session.Id.ToString() });

        sessionCount.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_ShouldNotThrow_WhenCalledTwiceSequentially()
    {
        await using var context = new SqliteContext(":memory:", NullLogger<SqliteContext>.Instance);
        await context.InitializeSchemaAsync();

        var repo = new ActivitySessionRepository(
            context,
            new Mock<IOutboxRepository>().Object,
            NullLogger<ActivitySessionRepository>.Instance);

        var now = DateTime.UtcNow;
        var session = CreateSession(now);

        await repo.SaveWithOutboxAsync(
            session,
            new[] { OutboxItem.Create("activity_session", session.Id, "{\"ok\":true}", $"k:{session.Id}:seed") });

        session.Extend(now.AddSeconds(10));
        var act = async () =>
        {
            await repo.UpdateAsync(session);
            session.Extend(now.AddSeconds(20));
            await repo.UpdateAsync(session);
        };

        await act.Should().NotThrowAsync();
    }

    private static ActivitySession CreateSession(DateTime now)
    {
        var userId = Guid.NewGuid();
        var app = new AppIdentity("deadbeefdeadbeef", "Test App", AppCategory.Unknown);
        var period = new TimeRange(now, now.AddSeconds(5));

        return new ActivitySession(
            Guid.NewGuid(),
            userId,
            app,
            period,
            windowHash: "wh",
            windowTitle: "title");
    }
}

