using FluentAssertions;
using Moq;
using TimeTrack.Agent.Contracts.Configuration;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Contracts.Updates;
using TimeTrack.Agent.Infrastructure.Services;
using Xunit;

namespace TimeTrack.Agent.Tests.Update;

public sealed class UpdateHttpClientTests
{
    [Fact]
    public void Constructor_ShouldSetBaseAddress_FromUpdateUrl()
    {
        // Arrange
        var settings = new UpdateSettings
        {
            UpdateUrl = "https://api.example.com/api/v1/updates",
            DownloadTimeoutMinutes = 5
        };

        // Act
        using var httpClient = new HttpClient();
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger<UpdateHttpClient>>().Object;
        var sut = new UpdateHttpClient(httpClient, settings, logger);

        // Assert
        httpClient.BaseAddress.Should().NotBeNull();
        httpClient.BaseAddress!.ToString().Should().Be("https://api.example.com/");
    }

    [Fact]
    public void Constructor_ShouldSetTimeout_FromSettings()
    {
        // Arrange
        var settings = new UpdateSettings
        {
            UpdateUrl = "https://api.example.com/api/v1/updates",
            DownloadTimeoutMinutes = 10
        };

        // Act
        using var httpClient = new HttpClient();
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger<UpdateHttpClient>>().Object;
        var sut = new UpdateHttpClient(httpClient, settings, logger);

        // Assert
        httpClient.Timeout.Should().Be(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void Constructor_WithNullSettings_ShouldThrowArgumentNullException()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger<UpdateHttpClient>>().Object;

        // Act
        var act = () => new UpdateHttpClient(httpClient, null!, logger);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("settings");
    }

    [Fact]
    public void Constructor_WithNullHttpClient_ShouldThrowArgumentNullException()
    {
        // Arrange
        var settings = new UpdateSettings { UpdateUrl = "https://api.example.com/api/v1/updates" };
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger<UpdateHttpClient>>().Object;

        // Act
        var act = () => new UpdateHttpClient(null!, settings, logger);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("httpClient");
    }
}
