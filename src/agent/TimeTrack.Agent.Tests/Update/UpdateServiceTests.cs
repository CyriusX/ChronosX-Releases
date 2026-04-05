using FluentAssertions;
using Moq;
using TimeTrack.Agent.Application.Services;
using TimeTrack.Agent.Contracts.Configuration;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Contracts.Updates;
using Xunit;

namespace TimeTrack.Agent.Tests.Update;

public sealed class UpdateServiceTests : IDisposable
{
    private readonly Mock<IUpdateHttpClient> _httpClientMock;
    private readonly UpdateSettings _settings;
    private readonly UpdateService _sut;

    public UpdateServiceTests()
    {
        _httpClientMock = new Mock<IUpdateHttpClient>();
        _settings = new UpdateSettings
        {
            Enabled = true,
            Channel = "stable",
            CheckIntervalHours = 4,
            UpdateUrl = "https://api.example.com/api/v1/updates",
            VerifySignature = false
        };

        _sut = new UpdateService(
            _httpClientMock.Object,
            _settings,
            new Mock<Microsoft.Extensions.Logging.ILogger<UpdateService>>().Object);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenUpdateAvailable_ShouldReturnUpdateInfo()
    {
        // Arrange
        var expectedResponse = new UpdateCheckResponse
        {
            HasUpdate = true,
            CurrentVersion = "1.0.0",
            LatestVersion = "1.1.0",
            DownloadUrl = "https://example.com/setup.exe"
        };

        _httpClientMock
            .Setup(x => x.CheckForUpdatesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _sut.CheckForUpdatesAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.HasUpdate.Should().BeTrue();
        result!.LatestVersion.Should().Be("1.1.0");
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenNoUpdate_ShouldReturnNoUpdate()
    {
        // Arrange
        var expectedResponse = new UpdateCheckResponse
        {
            HasUpdate = false,
            CurrentVersion = "1.1.0"
        };

        _httpClientMock
            .Setup(x => x.CheckForUpdatesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _sut.CheckForUpdatesAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.HasUpdate.Should().BeFalse();
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WhenNetworkError_ShouldReturnNull()
    {
        // Arrange
        _httpClientMock
            .Setup(x => x.CheckForUpdatesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await _sut.CheckForUpdatesAsync(CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void IsUpdating_Initially_ShouldBeFalse()
    {
        // Assert
        _sut.IsUpdating.Should().BeFalse();
    }

    [Fact]
    public async Task ProgressChangedEvent_ShouldBeRaised_WhenUpdateProgresses()
    {
        // Arrange
        UpdateProgress? capturedProgress = null;
        _sut.ProgressChanged += (_, progress) => capturedProgress = progress;

        _httpClientMock
            .Setup(x => x.CheckForUpdatesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateCheckResponse { HasUpdate = false, CurrentVersion = "1.0.0" });

        // Act
        await _sut.CheckForUpdatesAsync(CancellationToken.None);

        // Assert
        capturedProgress.Should().NotBeNull();
        capturedProgress!.Stage.Should().Be(UpdateStage.Checking);
    }

    public void Dispose()
    {
        _sut.Dispose();
    }
}
