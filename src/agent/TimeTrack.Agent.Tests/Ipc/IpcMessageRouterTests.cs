using FluentAssertions;
using Moq;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.AgentService.Ipc;
using TimeTrack.AgentService.Ipc.Handlers;
using Xunit;

namespace TimeTrack.Agent.Tests.Ipc;

public sealed class IpcMessageRouterTests
{
    [Fact]
    public async Task HandleCommandAsync_ShouldReportException_WhenHandlerThrows()
    {
        var reporter = new Mock<IExceptionReporter>(MockBehavior.Strict);
        reporter
            .Setup(r => r.ReportAsync(It.IsAny<ExceptionReport>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var logger = new Mock<Microsoft.Extensions.Logging.ILogger<IpcMessageRouter>>().Object;
        var router = new IpcMessageRouter(
            commands: new[] { new ThrowingCommandHandler() },
            queries: Array.Empty<IIpcQueryHandler>(),
            exceptionReporter: reporter.Object,
            logger: logger);

        var request = new IpcRequest
        {
            RequestId = 123,
            Type = "command",
            Name = "Throwing",
            Payload = null
        };

        var response = await router.HandleCommandAsync(request, CancellationToken.None);

        response.Success.Should().BeFalse();
        response.RequestId.Should().Be(123);
        response.Error.Should().Be("boom");

        reporter.Verify(r => r.ReportAsync(
            It.Is<ExceptionReport>(rep =>
                rep.Component == "AgentService" &&
                rep.Operation == "ipc.command.Throwing"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private sealed class ThrowingCommandHandler : IIpcCommandHandler
    {
        public string CommandName => "Throwing";

        public Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
            => throw new InvalidOperationException("boom");
    }
}

