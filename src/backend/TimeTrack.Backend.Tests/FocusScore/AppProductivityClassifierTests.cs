using FluentAssertions;
using TimeTrack.Backend.Application.FocusScore;
using TimeTrack.Backend.Domain.ValueObjects;
using Xunit;

namespace TimeTrack.Backend.Tests.FocusScore;

/// <summary>
/// Unit tests for AppProductivityClassifier
///
/// SRP: Each test verifies a single classification rule
/// </summary>
public sealed class AppProductivityClassifierTests
{
    private readonly AppProductivityClassifier _classifier;

    public AppProductivityClassifierTests()
    {
        _classifier = new AppProductivityClassifier();
    }

    // ============================================================================
    // Productive Apps Tests
    // ============================================================================

    [Theory]
    [InlineData("code")]
    [InlineData("vscode")]
    [InlineData("Code")]
    [InlineData("VSCODE")]
    [InlineData("idea64")]
    [InlineData("figma")]
    [InlineData("notion")]
    [InlineData("excel")]
    [InlineData("word")]
    [InlineData("terminal")]
    [InlineData("cmd")]
    [InlineData("postman")]
    public void Classify_ProductiveApps_ReturnsProductive(string processName)
    {
        // Act
        var category = _classifier.Classify(processName);

        // Assert
        category.Should().Be(AppProductivityCategory.Productive);
    }

    [Fact]
    public void Classify_VSCode_ReturnsProductive()
    {
        // Act
        var category = _classifier.Classify("Code.exe");

        // Assert
        category.Should().Be(AppProductivityCategory.Productive);
    }

    // ============================================================================
    // Distraction Apps Tests
    // ============================================================================

    [Theory]
    [InlineData("spotify")]
    [InlineData("youtube")]
    [InlineData("Steam")]
    [InlineData("instagram")]
    [InlineData("whatsapp")]
    [InlineData("telegram")]
    [InlineData("facebook")]
    [InlineData("twitter")]
    [InlineData("tiktok")]
    [InlineData("netflix")]
    public void Classify_DistractionApps_ReturnsDistraction(string processName)
    {
        // Act
        var category = _classifier.Classify(processName);

        // Assert
        category.Should().Be(AppProductivityCategory.Distraction);
    }

    // ============================================================================
    // Neutral Apps Tests
    // ============================================================================

    [Theory]
    [InlineData("chrome")]
    [InlineData("firefox")]
    [InlineData("msedge")]
    [InlineData("explorer")]
    public void Classify_NeutralApps_ReturnsNeutral(string processName)
    {
        // Act
        var category = _classifier.Classify(processName);

        // Assert
        category.Should().Be(AppProductivityCategory.Neutral);
    }

    // ============================================================================
    // Context-Aware Classification Tests
    // ============================================================================

    [Fact]
    public void ClassifyWithContext_ChromeWithGitHub_ReturnsProductive()
    {
        // Act
        var category = _classifier.ClassifyWithContext("chrome", "GitHub - MyRepo");

        // Assert
        category.Should().Be(AppProductivityCategory.Productive);
    }

    [Fact]
    public void ClassifyWithContext_ChromeWithStackOverflow_ReturnsProductive()
    {
        // Act
        var category = _classifier.ClassifyWithContext("chrome", "Stack Overflow - Question");

        // Assert
        category.Should().Be(AppProductivityCategory.Productive);
    }

    [Fact]
    public void ClassifyWithContext_ChromeWithLocalhost_ReturnsProductive()
    {
        // Act
        var category = _classifier.ClassifyWithContext("chrome", "localhost:3000 - React App");

        // Assert
        category.Should().Be(AppProductivityCategory.Productive);
    }

    [Fact]
    public void ClassifyWithContext_ChromeWithDocs_ReturnsProductive()
    {
        // Act
        var category = _classifier.ClassifyWithContext("chrome", "docs.microsoft.com - API Reference");

        // Assert
        category.Should().Be(AppProductivityCategory.Productive);
    }

    [Fact]
    public void ClassifyWithContext_ChromeWithYouTube_ReturnsDistraction()
    {
        // Act
        var category = _classifier.ClassifyWithContext("chrome", "YouTube - Funny Cat Video");

        // Assert
        category.Should().Be(AppProductivityCategory.Distraction);
    }

    [Fact]
    public void ClassifyWithContext_ChromeWithNetflix_ReturnsDistraction()
    {
        // Act
        var category = _classifier.ClassifyWithContext("chrome", "Netflix - Stranger Things");

        // Assert
        category.Should().Be(AppProductivityCategory.Distraction);
    }

    [Fact]
    public void ClassifyWithContext_ChromeWithFacebook_ReturnsDistraction()
    {
        // Act
        var category = _classifier.ClassifyWithContext("chrome", "Facebook - News Feed");

        // Assert
        category.Should().Be(AppProductivityCategory.Distraction);
    }

    [Fact]
    public void ClassifyWithContext_ChromeWithGenericTitle_ReturnsNeutral()
    {
        // Act
        var category = _classifier.ClassifyWithContext("chrome", "Some Random Website");

        // Assert
        category.Should().Be(AppProductivityCategory.Neutral);
    }

    [Fact]
    public void ClassifyWithContext_NonBrowserApp_IgnoresWindowTitle()
    {
        // Act - Spotify is distraction regardless of window title
        var category = _classifier.ClassifyWithContext("spotify", "Some Song - Artist");

        // Assert
        category.Should().Be(AppProductivityCategory.Distraction);
    }

    // ============================================================================
    // Edge Cases Tests
    // ============================================================================

    [Fact]
    public void Classify_EmptyString_ReturnsNeutral()
    {
        // Act
        var category = _classifier.Classify("");

        // Assert
        category.Should().Be(AppProductivityCategory.Neutral);
    }

    [Fact]
    public void Classify_Null_ReturnsNeutral()
    {
        // Act
        var category = _classifier.Classify(null!);

        // Assert
        category.Should().Be(AppProductivityCategory.Neutral);
    }

    [Fact]
    public void Classify_Whitespace_ReturnsNeutral()
    {
        // Act
        var category = _classifier.Classify("   ");

        // Assert
        category.Should().Be(AppProductivityCategory.Neutral);
    }

    [Fact]
    public void Classify_UnknownApp_ReturnsNeutral()
    {
        // Act
        var category = _classifier.Classify("SomeUnknownApp12345");

        // Assert
        category.Should().Be(AppProductivityCategory.Neutral);
    }

    [Fact]
    public void Classify_WithExeExtension_RemovesExtensionAndClassifies()
    {
        // Act
        var category = _classifier.Classify("Code.exe");

        // Assert
        category.Should().Be(AppProductivityCategory.Productive);
    }

    // ============================================================================
    // Custom Lists Tests
    // ============================================================================

    [Fact]
    public void Constructor_WithCustomProductiveApps_UsesCustomList()
    {
        // Arrange
        var customClassifier = new AppProductivityClassifier(
            productiveApps: new[] { "my-custom-app" },
            distractionApps: null,
            neutralApps: null,
            excludedApps: null);

        // Act
        var category = customClassifier.Classify("my-custom-app");

        // Assert
        category.Should().Be(AppProductivityCategory.Productive);
    }

    [Fact]
    public void Constructor_WithCustomDistractionApps_UsesCustomList()
    {
        // Arrange
        var customClassifier = new AppProductivityClassifier(
            productiveApps: null,
            distractionApps: new[] { "my-game" },
            neutralApps: null,
            excludedApps: null);

        // Act
        var category = customClassifier.Classify("my-game");

        // Assert
        category.Should().Be(AppProductivityCategory.Distraction);
    }

    [Fact]
    public void Constructor_WithExcludedApps_TreatsAsNeutral()
    {
        // Arrange
        var customClassifier = new AppProductivityClassifier(
            productiveApps: null,
            distractionApps: null,
            neutralApps: null,
            excludedApps: new[] { "spotify" });

        // Act
        var category = customClassifier.Classify("spotify");

        // Assert
        category.Should().Be(AppProductivityCategory.Neutral);
    }

    // ============================================================================
    // Partial Match Tests
    // ============================================================================

    [Fact]
    public void Classify_AppContainingCode_ReturnsProductive()
    {
        // Act
        var category = _classifier.Classify("my-custom-code-editor");

        // Assert
        category.Should().Be(AppProductivityCategory.Productive);
    }

    [Fact]
    public void Classify_AppContainingGame_ReturnsDistraction()
    {
        // Act
        var category = _classifier.Classify("my-game-launcher");

        // Assert
        category.Should().Be(AppProductivityCategory.Distraction);
    }
}
