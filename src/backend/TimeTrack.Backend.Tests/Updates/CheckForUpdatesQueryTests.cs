using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using TimeTrack.Backend.Application.Updates.DTOs;
using TimeTrack.Backend.Application.Updates.Queries;
using Xunit;

namespace TimeTrack.Backend.Tests.Updates;

/// <summary>
/// Unit tests for CheckForUpdatesQuery
/// </summary>
public sealed class CheckForUpdatesQueryTests
{
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ILogger<CheckForUpdatesQueryHandler>> _loggerMock;
    private readonly CheckForUpdatesQueryHandler _handler;

    public CheckForUpdatesQueryTests()
    {
        _configurationMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<CheckForUpdatesQueryHandler>>();
        _handler = new CheckForUpdatesQueryHandler(_configurationMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WhenUpdateAvailable_ShouldReturnUpdateInfo()
    {
        // Arrange
        SetupUpdateConfiguration(
            latestVersion: "1.1.0",
            downloadUrl: "https://cdn.example.com/setup-1.1.0.exe",
            checksumSha256: "abc123",
            fileSizeBytes: 85000000,
            releaseNotes: "## What's New\n- Bug fixes",
            channels: new[] { "stable", "beta" });

        var query = new CheckForUpdatesQuery("1.0.0", "stable");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.HasUpdate.Should().BeTrue();
        result.LatestVersion.Should().Be("1.1.0");
        result.CurrentVersion.Should().Be("1.0.0");
        result.DownloadUrl.Should().Be("https://cdn.example.com/setup-1.1.0.exe");
        result.ChecksumSha256.Should().Be("abc123");
        result.FileSizeBytes.Should().Be(85000000);
        result.ReleaseNotes.Should().Contain("Bug fixes");
    }

    [Fact]
    public async Task Handle_WhenNoUpdateAvailable_ShouldReturnNull()
    {
        // Arrange
        SetupUpdateConfiguration(latestVersion: "1.0.0");

        var query = new CheckForUpdatesQuery("1.0.0", "stable");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenCurrentVersionIsHigher_ShouldReturnNull()
    {
        // Arrange
        SetupUpdateConfiguration(latestVersion: "1.0.0");

        var query = new CheckForUpdatesQuery("1.1.0", "stable");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenInvalidChannel_ShouldReturnNull()
    {
        // Arrange
        SetupUpdateConfiguration(
            latestVersion: "1.1.0",
            channels: new[] { "stable" });

        var query = new CheckForUpdatesQuery("1.0.0", "beta");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenNoVersionConfigured_ShouldReturnNull()
    {
        // Arrange
        SetupUpdateConfiguration(latestVersion: null);

        var query = new CheckForUpdatesQuery("1.0.0", "stable");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenMandatoryUpdate_ShouldSetIsMandatoryFlag()
    {
        // Arrange
        SetupUpdateConfiguration(
            latestVersion: "1.1.0",
            isMandatory: true);

        var query = new CheckForUpdatesQuery("1.0.0", "stable");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.IsMandatory.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenMinimumVersionSet_ShouldSetIsMandatoryFlag()
    {
        // Arrange
        SetupUpdateConfiguration(
            latestVersion: "1.1.0",
            minimumVersion: "1.0.5");

        var query = new CheckForUpdatesQuery("1.0.0", "stable");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.IsMandatory.Should().BeTrue();
        result.MinimumVersion.Should().Be("1.0.5");
    }

    [Fact]
    public async Task Handle_WithVersionPrefix_ShouldCompareCorrectly()
    {
        // Arrange
        SetupUpdateConfiguration(latestVersion: "v1.1.0");

        var query = new CheckForUpdatesQuery("v1.0.0", "stable");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.HasUpdate.Should().BeTrue();
        result.LatestVersion.Should().Be("v1.1.0");
    }

    [Fact]
    public async Task Handle_WithBetaChannel_ShouldWorkWhenChannelSupported()
    {
        // Arrange
        SetupUpdateConfiguration(
            latestVersion: "1.2.0-beta.1",
            channels: new[] { "stable", "beta" });

        var query = new CheckForUpdatesQuery("1.1.0", "beta");

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.HasUpdate.Should().BeTrue();
        result.LatestVersion.Should().Be("1.2.0-beta.1");
    }

    private void SetupUpdateConfiguration(
        string? latestVersion = "1.0.0",
        string? downloadUrl = null,
        string? checksumSha256 = null,
        long? fileSizeBytes = null,
        string? releaseNotes = null,
        string? minimumVersion = null,
        bool isMandatory = false,
        string[]? channels = null)
    {
        var updatesSectionMock = new Mock<IConfigurationSection>();
        updatesSectionMock.Setup(x => x.Key).Returns("Updates");

        SetupSectionValue(updatesSectionMock, "LatestVersion", latestVersion);
        SetupSectionValue(updatesSectionMock, "DownloadUrl", downloadUrl);
        SetupSectionValue(updatesSectionMock, "ChecksumSha256", checksumSha256);
        SetupSectionValue(updatesSectionMock, "FileSizeBytes", fileSizeBytes?.ToString());
        SetupSectionValue(updatesSectionMock, "ReleaseNotes", releaseNotes);
        SetupSectionValue(updatesSectionMock, "MinimumVersion", minimumVersion);
        SetupSectionValue(updatesSectionMock, "IsMandatory", isMandatory.ToString().ToLowerInvariant());

        // Setup Channels section
        var channelsSectionMock = new Mock<IConfigurationSection>();
        channelsSectionMock.Setup(x => x.Key).Returns("Channels");

        if (channels != null)
        {
            var children = channels.Select((c, i) =>
            {
                var childMock = new Mock<IConfigurationSection>();
                childMock.Setup(x => x.Key).Returns(i.ToString());
                childMock.Setup(x => x.Value).Returns(c);
                return childMock.Object;
            }).ToList();

            channelsSectionMock.Setup(x => x.GetChildren()).Returns(children);
        }

        updatesSectionMock.Setup(x => x.GetSection("Channels"))
            .Returns(channelsSectionMock.Object);

        _configurationMock.Setup(x => x.GetSection("Updates"))
            .Returns(updatesSectionMock.Object);
    }

    private static void SetupSectionValue(Mock<IConfigurationSection> sectionMock, string key, string? value)
    {
        var childMock = new Mock<IConfigurationSection>();
        childMock.Setup(x => x.Key).Returns(key);
        childMock.Setup(x => x.Value).Returns(value);
        sectionMock.Setup(x => x.GetSection(key)).Returns(childMock.Object);
    }
}
