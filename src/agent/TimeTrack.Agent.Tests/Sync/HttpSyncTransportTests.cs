using System.Net;
using System.Text.Json;
using FluentAssertions;
using Moq;
using TimeTrack.Agent.Contracts.Configuration;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Domain.Entities;
using TimeTrack.Agent.Infrastructure.Services;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace TimeTrack.Agent.Tests.Sync;

public sealed class HttpSyncTransportTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly Mock<IOutboxRepository> _outboxRepositoryMock;
    private readonly SyncSettings _settings;
    private readonly JsonSerializerOptions _jsonOptions;

    public HttpSyncTransportTests()
    {
        _server = WireMockServer.Start();
        _outboxRepositoryMock = new Mock<IOutboxRepository>();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        _settings = new SyncSettings
        {
            BackendUrl = _server.Url!,
            SyncIntervalSeconds = 60,
            MaxBatchSize = 100,
            MaxBatchSizeBytes = 1024 * 1024,
            HttpTimeoutSeconds = 30
        };
    }

    [Fact]
    public async Task SendActivitySessionsAsync_ShouldReturnSuccess_WhenServerReturns200()
    {
        // Arrange
        var items = CreateTestOutboxItems(3);

        _server.Given(Request.Create()
                .WithPath("/api/v1/ingest/activity-sessions")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new { processed = 3, duplicates = 0, errors = Array.Empty<object>() }));

        var httpClient = new HttpClient();
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger<HttpSyncTransport>>().Object;
        var transport = new HttpSyncTransport(httpClient, _settings, logger);

        // Act
        var result = await transport.SendActivitySessionsAsync(items);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ProcessedCount.Should().Be(3);
        result.DuplicatesCount.Should().Be(0);
    }

    [Fact]
    public async Task SendActivitySessionsAsync_ShouldReturnFailure_WhenServerReturns500()
    {
        // Arrange
        var items = CreateTestOutboxItems(3);

        _server.Given(Request.Create()
                .WithPath("/api/v1/ingest/activity-sessions")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(500)
                .WithBodyAsJson(new { error = "Internal server error" }));

        var httpClient = new HttpClient();
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger<HttpSyncTransport>>().Object;
        var transport = new HttpSyncTransport(httpClient, _settings, logger);

        // Act
        var result = await transport.SendActivitySessionsAsync(items);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task SendActivitySessionsAsync_ShouldReturnFailure_WhenServerReturns400()
    {
        // Arrange
        var items = CreateTestOutboxItems(150); // Exceeds batch limit

        _server.Given(Request.Create()
                .WithPath("/api/v1/ingest/activity-sessions")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(400)
                .WithBodyAsJson(new { error = "Batch size exceeds limit" }));

        var httpClient = new HttpClient();
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger<HttpSyncTransport>>().Object;
        var transport = new HttpSyncTransport(httpClient, _settings, logger);

        // Act
        var result = await transport.SendActivitySessionsAsync(items);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task SendActivitySessionsAsync_ShouldHandleTimeout()
    {
        // Arrange
        var items = CreateTestOutboxItems(3);
        var shortTimeoutSettings = new SyncSettings
        {
            BackendUrl = _server.Url!,
            HttpTimeoutSeconds = 1 // 1 second timeout
        };

        _server.Given(Request.Create()
                .WithPath("/api/v1/ingest/activity-sessions")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithDelay(TimeSpan.FromSeconds(5))); // 5 second delay

        var httpClient = new HttpClient();
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger<HttpSyncTransport>>().Object;
        var transport = new HttpSyncTransport(httpClient, shortTimeoutSettings, logger);

        // Act
        var result = await transport.SendActivitySessionsAsync(items);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Contain("timeout", "error message should mention timeout");
    }

    [Fact]
    public async Task SendIdlePeriodsAsync_ShouldReturnSuccess_WhenServerReturns200()
    {
        // Arrange
        var items = CreateIdlePeriodOutboxItems(3);

        _server.Given(Request.Create()
                .WithPath("/api/v1/ingest/idle-periods")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new { processed = 3, duplicates = 0, errors = Array.Empty<object>() }));

        var httpClient = new HttpClient();
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger<HttpSyncTransport>>().Object;
        var transport = new HttpSyncTransport(httpClient, _settings, logger);

        // Act
        var result = await transport.SendIdlePeriodsAsync(items);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ProcessedCount.Should().Be(3);
    }

    [Fact]
    public async Task CheckHealthAsync_ShouldReturnTrue_WhenServerIsHealthy()
    {
        // Arrange
        _server.Given(Request.Create()
                .WithPath("/health")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new { status = "Healthy" }));

        var httpClient = new HttpClient();
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger<HttpSyncTransport>>().Object;
        var transport = new HttpSyncTransport(httpClient, _settings, logger);

        // Act
        var result = await transport.CheckHealthAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CheckHealthAsync_ShouldReturnFalse_WhenServerIsUnhealthy()
    {
        // Arrange
        _server.Given(Request.Create()
                .WithPath("/health")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(503));

        var httpClient = new HttpClient();
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger<HttpSyncTransport>>().Object;
        var transport = new HttpSyncTransport(httpClient, _settings, logger);

        // Act
        var result = await transport.CheckHealthAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SendActivitySessionsAsync_ShouldHandleDuplicateResponse()
    {
        // Arrange
        var items = CreateTestOutboxItems(5);

        _server.Given(Request.Create()
                .WithPath("/api/v1/ingest/activity-sessions")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new { processed = 3, duplicates = 2, errors = Array.Empty<object>() }));

        var httpClient = new HttpClient();
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger<HttpSyncTransport>>().Object;
        var transport = new HttpSyncTransport(httpClient, _settings, logger);

        // Act
        var result = await transport.SendActivitySessionsAsync(items);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ProcessedCount.Should().Be(3);
        result.DuplicatesCount.Should().Be(2);
    }

    private List<OutboxItem> CreateTestOutboxItems(int count)
    {
        var items = new List<OutboxItem>();
        for (int i = 0; i < count; i++)
        {
            var payload = JsonSerializer.Serialize(new
            {
                ProcessName = $"app_{i}",
                WindowTitle = $"Window {i}",
                AppCategory = "Productivity",
                StartedAt = DateTime.UtcNow.AddMinutes(-i - 1),
                EndedAt = DateTime.UtcNow.AddMinutes(-i)
            });

            var item = OutboxItem.Create(
                "activity_session",
                Guid.NewGuid(),
                payload,
                $"idempotency_key_{Guid.NewGuid()}");

            items.Add(item);
        }
        return items;
    }

    private List<OutboxItem> CreateIdlePeriodOutboxItems(int count)
    {
        var items = new List<OutboxItem>();
        for (int i = 0; i < count; i++)
        {
            var payload = JsonSerializer.Serialize(new
            {
                StartedAt = DateTime.UtcNow.AddMinutes(-i - 1),
                EndedAt = DateTime.UtcNow.AddMinutes(-i)
            });

            var item = OutboxItem.Create(
                "idle_period",
                Guid.NewGuid(),
                payload,
                $"idempotency_key_idle_{Guid.NewGuid()}");

            items.Add(item);
        }
        return items;
    }

    public void Dispose()
    {
        _server.Stop();
        _server.Dispose();
    }
}
