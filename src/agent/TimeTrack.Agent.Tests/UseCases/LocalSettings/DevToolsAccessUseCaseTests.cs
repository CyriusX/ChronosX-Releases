using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using TimeTrack.Agent.Application.UseCases.LocalSettings;
using TimeTrack.Agent.Contracts.Repositories;
using LocalSettingsEntity = TimeTrack.Agent.Domain.Entities.LocalSettings;
using Xunit;

namespace TimeTrack.Agent.Tests.UseCases;

public sealed class DevToolsAccessUseCaseTests
{
    [Fact]
    public async Task GetAsync_ShouldAutoDisableDevTools_WhenExpiryHasPassed()
    {
        var settings = LocalSettingsEntity.CreateDefault()
            .WithDevToolsAccess(true, DateTime.UtcNow.AddHours(1));

        // Simulate time passing: expiry is now in the past, but DevToolsEnabled is still true in storage.
        SetPrivateProperty(settings, nameof(LocalSettingsEntity.DevToolsEnabledUntilUtc), DateTime.UtcNow.AddMinutes(-5));

        var repo = new Mock<ILocalSettingsRepository>();
        repo.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        LocalSettingsEntity? saved = null;
        repo.Setup(r => r.SaveAsync(It.IsAny<LocalSettingsEntity>(), It.IsAny<CancellationToken>()))
            .Callback<LocalSettingsEntity, CancellationToken>((s, _) => saved = s)
            .Returns(Task.CompletedTask);

        var useCase = new LocalSettingsUseCase(repo.Object);
        var result = await useCase.GetAsync(CancellationToken.None);

        result.DevToolsEnabled.Should().BeFalse();
        saved.Should().NotBeNull();
        saved!.DevToolsEnabled.Should().BeFalse();
        saved.DevToolsEnabledUntilUtc.Should().BeNull();
    }

    private static void SetPrivateProperty<T>(T instance, string propertyName, object? value)
    {
        var prop = typeof(T).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        prop.Should().NotBeNull($"Property {typeof(T).Name}.{propertyName} must exist");
        prop!.SetValue(instance, value);
    }
}
