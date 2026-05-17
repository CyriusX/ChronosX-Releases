using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TimeTrack.Agent.Contracts.Configuration;
using TimeTrack.Agent.Infrastructure.MacOS.Security;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace TimeTrack.Agent.Tests.MacOS.Security;

public sealed class KeychainTokenStoreRefreshTests : IDisposable
{
    private readonly WireMockServer _server;

    public KeychainTokenStoreRefreshTests()
    {
        _server = WireMockServer.Start();
    }

    [Fact]
    public async Task RefreshAsync_ShouldClearTokens_WhenServerReturns400()
    {
        if (!ShouldRunKeychainTests())
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
        if (!ShouldRunKeychainTests())
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
    public async Task RefreshAsync_ShouldCoalesceConcurrentCalls_ToSingleHttpRefresh()
    {
        if (!ShouldRunKeychainTests())
            return;

        _server.Given(Request.Create()
                .WithPath("/api/v1/auth/refresh")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new { accessToken = CreateJwtExpiringSoon(), refreshToken = "rotated-refresh-token" }));

        var store = CreateStore();
        await store.StoreTokensAsync(CreateJwtExpiringSoon(), "refresh-token");

        var results = await Task.WhenAll(
            store.RefreshAsync(),
            store.RefreshAsync());

        results.Should().Contain(true);
        _server.LogEntries.Should().HaveCount(1);

        await store.ClearAsync();
    }

    private KeychainTokenStore CreateStore()
    {
        var settings = new SyncSettings
        {
            BackendUrl = _server.Url!,
            HttpTimeoutSeconds = 10
        };

        var logger = new Mock<ILogger<KeychainTokenStore>>().Object;
        var httpClient = new HttpClient();

        var serviceName = $"com.cyriusx.timetrack.tests.{Guid.NewGuid():N}";
        var accountName = $"jwt_tokens_tests_{Guid.NewGuid():N}";

        return new KeychainTokenStore(logger, httpClient, settings, serviceNameOverride: serviceName, tokenAccountOverride: accountName);
    }

    private static bool ShouldRunKeychainTests()
    {
        if (!OperatingSystem.IsMacOS())
            return false;

        // These tests touch the user's Keychain. Require explicit opt-in.
        return string.Equals(
            Environment.GetEnvironmentVariable("TIMETRACK_ENABLE_KEYCHAIN_TESTS"),
            "1",
            StringComparison.Ordinal);
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

