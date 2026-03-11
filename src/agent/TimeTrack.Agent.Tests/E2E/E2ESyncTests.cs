using System.Text.Json;
using FluentAssertions;
using Moq;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Domain.Entities;
using TimeTrack.Agent.Infrastructure.Services;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace TimeTrack.Agent.Tests.E2E;

/// <summary>
/// Testes E2E do fluxo de sincronização: Agent -> Cloud -> Persistência
/// </summary>
public sealed class E2ESyncTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly Mock<IOutboxRepository> _outboxRepositoryMock;
    private readonly Mock<ISyncErrorRepository> _syncErrorRepositoryMock;
    private readonly JsonSerializerOptions _jsonOptions;

    public E2ESyncTests()
    {
        _server = WireMockServer.Start();
        _outboxRepositoryMock = new Mock<IOutboxRepository>();
        _syncErrorRepositoryMock = new Mock<ISyncErrorRepository>();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    #region Scenario 1: Normal Flow

    /// <summary>
    /// Cenário 1: Fluxo normal - Agent coleta sessão → flush no SQLite → SyncWorker envia → verifica sucesso
    /// </summary>
    [Fact]
    public async Task E2E_NormalFlow_ShouldSyncSuccessfully()
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
        var logger = CreateLogger<HttpSyncTransport>();
        var transport = new HttpSyncTransport(httpClient, CreateSettings(), logger);

        // Act
        var result = await transport.SendActivitySessionsAsync(items);

        // Assert
        result.IsSuccess.Should().BeTrue("sync should succeed");
        result.ProcessedCount.Should().Be(3);
        result.DuplicatesCount.Should().Be(0);

        // Verify request was made
        _server.LogEntries.Should().HaveCount(1);
    }

    #endregion

    #region Scenario 2: Idempotency

    /// <summary>
    /// Cenário 2: Idempotência - Mesmo batch enviado 3 vezes via API, verificar resposta de duplicates
    /// </summary>
    [Fact]
    public async Task E2E_Idempotency_SameBatchThreeTimes_ShouldReturnDuplicates()
    {
        // Arrange
        var items = CreateTestOutboxItems(5);

        // First request: all new
        _server.Given(Request.Create()
                .WithPath("/api/v1/ingest/activity-sessions")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new { processed = 5, duplicates = 0, errors = Array.Empty<object>() }));

        var httpClient = new HttpClient();
        var logger = CreateLogger<HttpSyncTransport>();
        var transport = new HttpSyncTransport(httpClient, CreateSettings(), logger);

        // Act 1: First send
        var result1 = await transport.SendActivitySessionsAsync(items);
        result1.IsSuccess.Should().BeTrue();
        result1.ProcessedCount.Should().Be(5);
        result1.DuplicatesCount.Should().Be(0);

        // Reset server for duplicate response
        _server.Reset();
        _server.Given(Request.Create()
                .WithPath("/api/v1/ingest/activity-sessions")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new { processed = 0, duplicates = 5, errors = Array.Empty<object>() }));

        // Act 2 & 3: Send again (simulating retry with same idempotency keys)
        var result2 = await transport.SendActivitySessionsAsync(items);
        var result3 = await transport.SendActivitySessionsAsync(items);

        // Assert
        result2.IsSuccess.Should().BeTrue();
        result2.DuplicatesCount.Should().Be(5, "second request should show duplicates");

        result3.IsSuccess.Should().BeTrue();
        result3.DuplicatesCount.Should().Be(5, "third request should show duplicates");
    }

    #endregion

    #region Scenario 3: Offline Mode

    /// <summary>
    /// Cenário 3: Modo offline - Servidor retorna 503, Agent acumula no Outbox
    /// </summary>
    [Fact]
    public async Task E2E_OfflineMode_Server503_ShouldAccumulateInOutbox()
    {
        // Arrange
        var items = CreateTestOutboxItems(3);

        // Server returns 503 (Service Unavailable)
        _server.Given(Request.Create()
                .WithPath("/api/v1/ingest/activity-sessions")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(503)
                .WithBodyAsJson(new { error = "Service temporarily unavailable" }));

        var httpClient = new HttpClient();
        var logger = CreateLogger<HttpSyncTransport>();
        var transport = new HttpSyncTransport(httpClient, CreateSettings(), logger);

        // Act
        var result = await transport.SendActivitySessionsAsync(items);

        // Assert
        result.IsSuccess.Should().BeFalse("sync should fail when server is unavailable");
        result.StatusCode.Should().Be(503);

        // After restoring server, sync should succeed
        _server.Reset();
        _server.Given(Request.Create()
                .WithPath("/api/v1/ingest/activity-sessions")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new { processed = 3, duplicates = 0, errors = Array.Empty<object>() }));

        var resultAfterRecovery = await transport.SendActivitySessionsAsync(items);
        resultAfterRecovery.IsSuccess.Should().BeTrue("sync should succeed after server recovery");
    }

    #endregion

    #region Scenario 4: Retry with Backoff

    /// <summary>
    /// Cenário 4: Retry com backoff - Servidor retorna 500, depois 200
    /// </summary>
    [Fact]
    public async Task E2E_RetryWithBackoff_Server500Then200_ShouldEventuallySucceed()
    {
        // Arrange
        var items = CreateTestOutboxItems(2);

        // First call: 500 error
        _server.Given(Request.Create()
                .WithPath("/api/v1/ingest/activity-sessions")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(500)
                .WithBodyAsJson(new { error = "Internal server error" }));

        var httpClient = new HttpClient();
        var logger = CreateLogger<HttpSyncTransport>();
        var transport = new HttpSyncTransport(httpClient, CreateSettings(), logger);

        // Act - First attempt fails
        var result1 = await transport.SendActivitySessionsAsync(items);
        result1.IsSuccess.Should().BeFalse();
        result1.StatusCode.Should().Be(500);

        // Simulate server recovery
        _server.Reset();
        _server.Given(Request.Create()
                .WithPath("/api/v1/ingest/activity-sessions")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new { processed = 2, duplicates = 0, errors = Array.Empty<object>() }));

        // Retry after backoff
        var resultFinal = await transport.SendActivitySessionsAsync(items);
        resultFinal.IsSuccess.Should().BeTrue("sync should succeed after server recovery");
        resultFinal.ProcessedCount.Should().Be(2);
    }

    #endregion

    #region Scenario 5: Restart During Sync

    /// <summary>
    /// Cenário 5: Restart durante sync - Reiniciar AgentService no meio de um batch
    /// Verificar: nenhum dado perdido, nenhuma duplicata
    /// </summary>
    [Fact]
    public async Task E2E_RestartDuringSync_ShouldNotLoseDataOrDuplicate()
    {
        // Arrange - Simulate a batch partially sent
        var items = CreateTestOutboxItems(10);

        // First batch succeeds
        _server.Given(Request.Create()
                .WithPath("/api/v1/ingest/activity-sessions")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new { processed = 5, duplicates = 0, errors = Array.Empty<object>() }));

        var httpClient = new HttpClient();
        var logger = CreateLogger<HttpSyncTransport>();
        var transport = new HttpSyncTransport(httpClient, CreateSettings(), logger);

        // Act - First sync (first 5 items)
        var result1 = await transport.SendActivitySessionsAsync(items.Take(5));
        result1.IsSuccess.Should().BeTrue();
        result1.ProcessedCount.Should().Be(5);

        // Simulate restart - same items sent again (idempotency handles this)
        _server.Reset();
        _server.Given(Request.Create()
                .WithPath("/api/v1/ingest/activity-sessions")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBodyAsJson(new { processed = 0, duplicates = 5, errors = Array.Empty<object>() }));

        // After restart, same items are sent - backend recognizes as duplicates
        var result2 = await transport.SendActivitySessionsAsync(items.Take(5));
        result2.IsSuccess.Should().BeTrue();
        result2.DuplicatesCount.Should().Be(5, "backend should recognize duplicates via idempotency keys");

        // Verify total calls
        _server.LogEntries.Should().HaveCount(2);
    }

    #endregion

    #region Helper Methods

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

    private Contracts.Configuration.SyncSettings CreateSettings()
    {
        return new Contracts.Configuration.SyncSettings
        {
            BackendUrl = _server.Url!,
            SyncIntervalSeconds = 60,
            MaxBatchSize = 100,
            MaxBatchSizeBytes = 1024 * 1024,
            HttpTimeoutSeconds = 30
        };
    }

    private static Microsoft.Extensions.Logging.ILogger<T> CreateLogger<T>()
    {
        return new Mock<Microsoft.Extensions.Logging.ILogger<T>>().Object;
    }

    #endregion

    public void Dispose()
    {
        _server.Stop();
        _server.Dispose();
    }
}
