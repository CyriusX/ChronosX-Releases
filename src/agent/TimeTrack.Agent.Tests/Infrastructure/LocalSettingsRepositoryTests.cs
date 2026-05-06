using Dapper;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TimeTrack.Agent.Infrastructure.Persistence;
using Xunit;

namespace TimeTrack.Agent.Tests.Infrastructure;

public sealed class LocalSettingsRepositoryTests
{
    [Fact]
    public async Task GetAsync_ShouldMapDevToolsEnabled_WhenColumnsAreSnakeCase()
    {
        await using var context = new SqliteContext(":memory:", NullLogger<SqliteContext>.Instance);
        await context.InitializeSchemaAsync();

        var repo = new LocalSettingsRepository(context, NullLogger<LocalSettingsRepository>.Instance);
        var connection = await context.GetConnectionAsync();

        var enabledUntilUtc = new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await connection.ExecuteAsync(@"
            INSERT OR REPLACE INTO local_settings
                (id, auto_resume_notification_enabled, notification_sounds_enabled, language, idle_threshold_seconds, work_goal_seconds, devtools_enabled, devtools_enabled_until_utc, updated_at)
            VALUES
                (@Id, @AutoResumeNotificationEnabled, @NotificationSoundsEnabled, @Language, @IdleThresholdSeconds, @WorkGoalSeconds, @DevToolsEnabled, @DevToolsEnabledUntilUtc, @UpdatedAt)",
            new
            {
                Id = "singleton",
                AutoResumeNotificationEnabled = 1,
                NotificationSoundsEnabled = 1,
                Language = "en-US",
                IdleThresholdSeconds = 180,
                WorkGoalSeconds = 28800,
                DevToolsEnabled = 1,
                DevToolsEnabledUntilUtc = enabledUntilUtc.ToString("O"),
                UpdatedAt = DateTime.UtcNow.ToString("O")
            });

        var settings = await repo.GetAsync();

        settings.DevToolsEnabled.Should().BeTrue();
        settings.DevToolsEnabledUntilUtc.Should().Be(enabledUntilUtc);
        settings.Language.Should().Be("en-US");
        settings.IdleThresholdSeconds.Should().Be(180);
        settings.WorkGoalSeconds.Should().Be(28800);
    }
}

