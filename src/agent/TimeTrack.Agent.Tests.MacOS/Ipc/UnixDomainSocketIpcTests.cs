using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using TimeTrack.AgentService.Ipc;
using TimeTrack.AgentService.Ipc.Handlers;
using TimeTrack.MacOSAgentService.Ipc;
using Xunit;

namespace TimeTrack.Agent.Tests.MacOS.Ipc;

public class UnixDomainSocketIpcTests : IAsyncLifetime
{
    private const string TestSocketPath = "/var/tmp/TimeTrack.Agent.IPC.Test";
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<ILogger<UnixDomainSocketIpcServer>> _serverLoggerMock;

    public UnixDomainSocketIpcTests()
    {
        _serviceProviderMock = new Mock<IServiceProvider>();
        _serverLoggerMock = new Mock<ILogger<UnixDomainSocketIpcServer>>();
    }

    public Task InitializeAsync()
    {
        if (File.Exists(TestSocketPath))
        {
            try { File.Delete(TestSocketPath); } catch { }
        }
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        if (File.Exists(TestSocketPath))
        {
            try { File.Delete(TestSocketPath); } catch { }
        }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Server_Should_StartListening_OnSocketPath()
    {
        var server = new UnixDomainSocketIpcServer(
            _serviceProviderMock.Object,
            _serverLoggerMock.Object,
            TestSocketPath);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var serverTask = server.StartAsync(cts.Token);

        await Task.Delay(500);

        server.IsListening.Should().BeTrue();
        File.Exists(TestSocketPath).Should().BeTrue();

        cts.Cancel();
        await server.StopAsync(CancellationToken.None);
        server.Dispose();
    }

    [Fact]
    public async Task Client_Should_ConnectToServer()
    {
        var router = new IpcMessageRouter(
            Array.Empty<IIpcCommandHandler>(),
            Array.Empty<IIpcQueryHandler>(),
            Mock.Of<ILogger<IpcMessageRouter>>());

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IpcMessageRouter)))
            .Returns(router);

        var server = new UnixDomainSocketIpcServer(
            _serviceProviderMock.Object,
            _serverLoggerMock.Object,
            TestSocketPath);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await server.StartAsync(cts.Token);
        await Task.Delay(500);

        using var clientSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
        var endpoint = new UnixDomainSocketEndPoint(TestSocketPath);
        await clientSocket.ConnectAsync(endpoint);

        // Connection is accepted asynchronously on the server loop.
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!server.IsClientConnected && DateTime.UtcNow < deadline)
        {
            await Task.Delay(25);
        }
        server.IsClientConnected.Should().BeTrue();

        cts.Cancel();
        await server.StopAsync(CancellationToken.None);
        server.Dispose();
    }

    [Fact]
    public async Task ClientServer_Should_ExchangeMessages()
    {
        var router = new IpcMessageRouter(
            Array.Empty<IIpcCommandHandler>(),
            Array.Empty<IIpcQueryHandler>(),
            Mock.Of<ILogger<IpcMessageRouter>>());

        _serviceProviderMock
            .Setup(sp => sp.GetService(typeof(IpcMessageRouter)))
            .Returns(router);

        var server = new UnixDomainSocketIpcServer(
            _serviceProviderMock.Object,
            _serverLoggerMock.Object,
            TestSocketPath);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await server.StartAsync(cts.Token);
        await Task.Delay(500);

        using var clientSocket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP);
        var endpoint = new UnixDomainSocketEndPoint(TestSocketPath);
        await clientSocket.ConnectAsync(endpoint);

        var stream = new NetworkStream(clientSocket);
        var reader = new StreamReader(stream, Encoding.UTF8);
        var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

        var request = new { requestId = 1, type = "query", name = "getTrackingState" };
        await writer.WriteLineAsync(JsonSerializer.Serialize(request));

        var responseLine = await reader.ReadLineAsync();
        responseLine.Should().NotBeNull();

        var response = JsonSerializer.Deserialize<JsonElement>(responseLine!);
        response.GetProperty("success").GetBoolean().Should().BeFalse();
        response.GetProperty("error").GetString().Should().Contain("Unknown query");

        cts.Cancel();
        await server.StopAsync(CancellationToken.None);
        server.Dispose();
    }
}
