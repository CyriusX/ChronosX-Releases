using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TimeTrack.Agent.Contracts.Configuration;
using TimeTrack.Agent.Infrastructure.Services;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace TimeTrack.Agent.Tests.Infrastructure;

public sealed class DpapiTokenStoreRefreshTests : IDisposable
{
    private readonly WireMockServer _server;

    public DpapiTokenStoreRefreshTests()
    {
        _server = WireMockServer.Start();
    }

    [Fact]
    public async Task RefreshAsync_ShouldClearTokens_WhenServerReturns400()
    {
        if (!OperatingSystem.IsWindows())
            return;

        _server.Given(Request.Create()
                .WithPath("/api/v1/auth/refresh")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(400)
                .WithBodyAsJson(new { code = "validation_failed" }));

        var store = CreateStore();
        await store.StoreTokensAsync(CreateJwtExpiringSoon(), "refresh-token");

        var cleared = false;
        store.TokensCleared += (_, _) => cleared = true;

        var refreshed = await store.RefreshAsync();

        refreshed.Should().BeFalse();
        cleared.Should().BeTrue();
        (await store.GetJwtAsync()).Should().BeNull();
        (await store.GetRefreshTokenAsync()).Should().BeNull();
    }

    [Fact]
    public async Task RefreshAsync_ShouldClearTokens_WhenServerReturns401()
    {
        if (!OperatingSystem.IsWindows())
            return;

        _server.Given(Request.Create()
                .WithPath("/api/v1/auth/refresh")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(401));

        var store = CreateStore();
        await store.StoreTokensAsync(CreateJwtExpiringSoon(), "refresh-token");

        var cleared = false;
        store.TokensCleared += (_, _) => cleared = true;

        var refreshed = await store.RefreshAsync();

        refreshed.Should().BeFalse();
        cleared.Should().BeTrue();
        (await store.GetJwtAsync()).Should().BeNull();
        (await store.GetRefreshTokenAsync()).Should().BeNull();
    }

    [Fact]
    public async Task RefreshAsync_ShouldKeepTokens_WhenServerReturns500()
    {
        if (!OperatingSystem.IsWindows())
            return;

        _server.Given(Request.Create()
                .WithPath("/api/v1/auth/refresh")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(500));

        var store = CreateStore();
        var jwt = CreateJwtExpiringSoon();
        await store.StoreTokensAsync(jwt, "refresh-token");

        var cleared = false;
        store.TokensCleared += (_, _) => cleared = true;

        var refreshed = await store.RefreshAsync();

        refreshed.Should().BeFalse();
        cleared.Should().BeFalse();
        (await store.GetJwtAsync()).Should().Be(jwt);
        (await store.GetRefreshTokenAsync()).Should().Be("refresh-token");
    }

    private DpapiTokenStore CreateStore()
    {
        var settings = new SyncSettings
        {
            BackendUrl = _server.Url!,
            HttpTimeoutSeconds = 10
        };

        var logger = new Mock<ILogger<DpapiTokenStore>>().Object;
        var httpClient = new HttpClient();

        var tokenFilePath = Path.Combine(
            Path.GetTempPath(),
            "TimeTrack.Agent.Tests",
            Guid.NewGuid().ToString("N"),
            "tokens.dat");

        return new DpapiTokenStore(logger, httpClient, settings, tokenFilePathOverride: tokenFilePath);
    }

    private static string CreateJwtExpiringSoon()
    {
        var exp = DateTimeOffset.UtcNow.AddSeconds(30).ToUnixTimeSeconds();
        var header = Base64EncodeJson(new { alg = "none", typ = "JWT" });
        var payload = Base64EncodeJson(new { exp });
        return $"{header}.{payload}.";
    }

    private static string Base64EncodeJson(object obj)
    {
        var json = JsonSerializer.Serialize(obj);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=');
    }

    public void Dispose()
    {
        _server.Stop();
        _server.Dispose();
    }
}

