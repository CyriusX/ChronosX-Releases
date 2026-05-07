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
    public async Task GetCurrentAsync_Should_ReturnCpuPercentWithinBounds()
    {
        var provider = new MacOSMachineMetricsProvider(_loggerMock.Object);

        var result = await provider.GetCurrentAsync();

        result.CpuPercent.Should().BeGreaterThanOrEqualTo(0);
        result.CpuPercent.Should().BeLessThanOrEqualTo(100);
    }

    [Fact]
    public async Task GetCurrentAsync_Should_ReturnMemoryMetrics()
    {
        var provider = new MacOSMachineMetricsProvider(_loggerMock.Object);

        var result = await provider.GetCurrentAsync();

        result.MemoryUsedMb.Should().BeGreaterThanOrEqualTo(0);
        result.MemoryTotalMb.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetCurrentAsync_Should_ReturnDiskMetrics()
    {
        var provider = new MacOSMachineMetricsProvider(_loggerMock.Object);

        var result = await provider.GetCurrentAsync();

        result.DiskUsedGb.Should().BeGreaterThanOrEqualTo(0);
        result.DiskTotalGb.Should().BeGreaterThanOrEqualTo(0);
    }
}
