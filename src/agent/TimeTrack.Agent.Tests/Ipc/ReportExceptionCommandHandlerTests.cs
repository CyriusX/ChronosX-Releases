using System.Text.Json;
using FluentAssertions;
using Moq;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.AgentService.Ipc;
using TimeTrack.AgentService.Ipc.Handlers.Commands.Diagnostics;
using Xunit;

namespace TimeTrack.Agent.Tests.Ipc;

public sealed class ReportExceptionCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_ShouldReturnSuccess_WhenPayloadIsValid()
    {
        var reporter = new Mock<IExceptionReporter>(MockBehavior.Strict);
        reporter
            .Setup(r => r.ReportAsync(It.IsAny<ExceptionReport>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var logger = new Mock<Microsoft.Extensions.Logging.ILogger<ReportExceptionCommandHandler>>().Object;
        var handler = new ReportExceptionCommandHandler(reporter.Object, logger);

        var report = ExceptionReport.FromException(new InvalidOperationException("boom"), "DesktopHost", "desktophost.test");
        var payload = JsonSerializer.SerializeToElement(report, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var request = new IpcRequest
        {
            RequestId = 1,
            Type = "command",
            Name = "ReportException",
            Payload = payload
        };

        var resp = await handler.HandleAsync(request, CancellationToken.None);

        resp.Success.Should().BeTrue();
        reporter.Verify(r => r.ReportAsync(
            It.Is<ExceptionReport>(rpt => rpt.Component == "DesktopHost" && rpt.Operation == "desktophost.test"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnValidationError_WhenPayloadMissing()
    {
        var reporter = new Mock<IExceptionReporter>(MockBehavior.Strict);
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger<ReportExceptionCommandHandler>>().Object;
        var handler = new ReportExceptionCommandHandler(reporter.Object, logger);

        var request = new IpcRequest
        {
            RequestId = 2,
            Type = "command",
            Name = "ReportException",
            Payload = null
        };

        var resp = await handler.HandleAsync(request, CancellationToken.None);

        resp.Success.Should().BeFalse();
        resp.Error.Should().NotBeNullOrWhiteSpace();
    }
}

