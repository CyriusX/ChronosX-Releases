using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TimeTrack.Agent.Infrastructure.MacOS.Providers;
using Xunit;

namespace TimeTrack.Agent.Tests.MacOS.Providers;

public class MacOSMachineMetricsProviderTests
{
    private readonly Mock<ILogger<MacOSMachineMetricsProvider>> _loggerMock;

    public MacOSMachineMetricsProviderTests()
    {
        _loggerMock = new Mock<ILogger<MacOSMachineMetricsProvider>>();
    }

    [Fact]
    public async Task GetCpuUsageAsync_Should_ReturnNonNegativeValue()
    {
        var provider = new MacOSMachineMetricsProvider(_loggerMock.Object);

        var result = await provider.GetCpuUsageAsync();

        result.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetMemoryUsageAsync_Should_ReturnNonNegativeValue()
    {
        var provider = new MacOSMachineMetricsProvider(_loggerMock.Object);

        var result = await provider.GetMemoryUsageAsync();

        result.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetDiskUsageAsync_Should_ReturnNonNegativeValue()
    {
        var provider = new MacOSMachineMetricsProvider(_loggerMock.Object);

        var result = await provider.GetDiskUsageAsync();

        result.Should().BeGreaterThanOrEqualTo(0);
    }
}
