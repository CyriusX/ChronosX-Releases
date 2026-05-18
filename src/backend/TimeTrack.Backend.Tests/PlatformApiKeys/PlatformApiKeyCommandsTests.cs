using FluentAssertions;
using Moq;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.PlatformApiKeys.Commands;
using TimeTrack.Backend.Application.PlatformApiKeys.Queries;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using Xunit;

namespace TimeTrack.Backend.Tests.PlatformApiKeys;

public sealed class PlatformApiKeyCommandsTests
{
    [Fact]
    public async Task Create_ShouldReturnTokenAndPersistHashedKey()
    {
        var repo = new Mock<IPlatformApiKeyRepository>();
        PlatformApiKey? saved = null;

        repo.Setup(r => r.AddAsync(It.IsAny<PlatformApiKey>(), It.IsAny<CancellationToken>()))
            .Callback<PlatformApiKey, CancellationToken>((k, _) => saved = k)
            .Returns(Task.CompletedTask);

        var hasher = new Mock<IPlatformApiKeyHasher>();
        hasher.Setup(h => h.GenerateToken()).Returns("tt_ops_token123");
        hasher.Setup(h => h.HashToken("tt_ops_token123")).Returns("hash123");

        var handler = new CreatePlatformApiKeyCommandHandler(repo.Object, hasher.Object);
        var result = await handler.Handle(new CreatePlatformApiKeyCommand("Ops bot"), CancellationToken.None);

        result.Token.Should().Be("tt_ops_token123");
        result.Label.Should().Be("Ops bot");
        saved.Should().NotBeNull();
        saved!.KeyHash.Should().Be("hash123");
    }

    [Fact]
    public async Task Create_WithEmptyLabel_ShouldThrowValidationException()
    {
        var repo = new Mock<IPlatformApiKeyRepository>();
        var hasher = new Mock<IPlatformApiKeyHasher>();
        var handler = new CreatePlatformApiKeyCommandHandler(repo.Object, hasher.Object);

        var act = () => handler.Handle(new CreatePlatformApiKeyCommand(" "), CancellationToken.None);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task List_ShouldReturnAllKeys()
    {
        var repo = new Mock<IPlatformApiKeyRepository>();
        var key1 = PlatformApiKey.Create(Guid.NewGuid(), "A", "h1");
        var key2 = PlatformApiKey.Create(Guid.NewGuid(), "B", "h2");
        repo.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { key1, key2 });

        var handler = new ListPlatformApiKeysQueryHandler(repo.Object);
        var result = await handler.Handle(new ListPlatformApiKeysQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(k => k.Label).Should().Contain(new[] { "A", "B" });
    }

    [Fact]
    public async Task Revoke_ShouldSetRevokedAtUtcAndUpdate()
    {
        var repo = new Mock<IPlatformApiKeyRepository>();
        var key = PlatformApiKey.Create(Guid.NewGuid(), "A", "h1");

        repo.Setup(r => r.GetByIdAsync(key.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(key);

        repo.Setup(r => r.UpdateAsync(It.IsAny<PlatformApiKey>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new RevokePlatformApiKeyCommandHandler(repo.Object);
        var result = await handler.Handle(new RevokePlatformApiKeyCommand(key.Id), CancellationToken.None);

        result.RevokedAtUtc.Should().NotBeNull();
        repo.Verify(r => r.UpdateAsync(key, It.IsAny<CancellationToken>()), Times.Once);
    }
}

