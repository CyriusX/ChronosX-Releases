using TimeTrack.Backend.Domain.ValueObjects;
using Xunit;

namespace TimeTrack.Backend.Tests.ValueObjects;

/// <summary>
/// Unit tests for IdentifierNormalizer
///
/// Tests cover:
/// - Normalization with explicit type (Exe/Domain)
/// - Normalization with inferred type
/// - Edge cases (empty, whitespace, special characters)
/// - Type inference logic
/// </summary>
public class IdentifierNormalizerTests
{
    // ========================================================================
    // Normalize with explicit Exe type
    // ========================================================================

    [Fact]
    public void Normalize_WithExeType_AddsExeExtension()
    {
        // Arrange
        var identifier = "chrome";
        var type = AppIdentifierType.Exe;

        // Act
        var result = IdentifierNormalizer.Normalize(identifier, type);

        // Assert
        Assert.Equal("chrome.exe", result);
    }

    [Fact]
    public void Normalize_WithExeTypeAndExistingExtension_KeepsExtension()
    {
        // Arrange
        var identifier = "chrome.exe";
        var type = AppIdentifierType.Exe;

        // Act
        var result = IdentifierNormalizer.Normalize(identifier, type);

        // Assert
        Assert.Equal("chrome.exe", result);
    }

    [Fact]
    public void Normalize_WithExeTypeAndNonStandardExtension_AddsExeExtension()
    {
        // Arrange
        var identifier = "myapp.app";
        var type = AppIdentifierType.Exe;

        // Act
        var result = IdentifierNormalizer.Normalize(identifier, type);

        // Assert
        Assert.Equal("myapp.app.exe", result);
    }

    [Fact]
    public void Normalize_WithExeType_ConvertsToLowerCase()
    {
        // Arrange
        var identifier = "CHROME";
        var type = AppIdentifierType.Exe;

        // Act
        var result = IdentifierNormalizer.Normalize(identifier, type);

        // Assert
        Assert.Equal("chrome.exe", result);
    }

    [Fact]
    public void Normalize_WithExeType_TrimsWhitespace()
    {
        // Arrange
        var identifier = "  chrome  ";
        var type = AppIdentifierType.Exe;

        // Act
        var result = IdentifierNormalizer.Normalize(identifier, type);

        // Assert
        Assert.Equal("chrome.exe", result);
    }

    // ========================================================================
    // Normalize with explicit Domain type
    // ========================================================================

    [Fact]
    public void Normalize_WithDomainType_KeepsIdentifierAsIs()
    {
        // Arrange
        var identifier = "github.com";
        var type = AppIdentifierType.Domain;

        // Act
        var result = IdentifierNormalizer.Normalize(identifier, type);

        // Assert
        Assert.Equal("github.com", result);
    }

    [Fact]
    public void Normalize_WithDomainType_DoesNotAddExeExtension()
    {
        // Arrange
        var identifier = "stackoverflow";
        var type = AppIdentifierType.Domain;

        // Act
        var result = IdentifierNormalizer.Normalize(identifier, type);

        // Assert
        Assert.Equal("stackoverflow", result);
    }

    [Fact]
    public void Normalize_WithDomainType_ConvertsToLowerCase()
    {
        // Arrange
        var identifier = "GITHUB.COM";
        var type = AppIdentifierType.Domain;

        // Act
        var result = IdentifierNormalizer.Normalize(identifier, type);

        // Assert
        Assert.Equal("github.com", result);
    }

    // ========================================================================
    // Normalize with inferred type (parameterless overload)
    // ========================================================================

    [Fact]
    public void Normalize_WithoutType_AddsExeForSimpleNames()
    {
        // Arrange
        var identifier = "chrome";

        // Act
        var result = IdentifierNormalizer.Normalize(identifier);

        // Assert
        Assert.Equal("chrome.exe", result);
    }

    [Fact]
    public void Normalize_WithoutType_KeepsDomainsWithDots()
    {
        // Arrange
        var identifier = "github.com";

        // Act
        var result = IdentifierNormalizer.Normalize(identifier);

        // Assert
        Assert.Equal("github.com", result);
    }

    [Fact]
    public void Normalize_WithoutType_KeepsExeWithExtension()
    {
        // Arrange
        var identifier = "chrome.exe";

        // Act
        var result = IdentifierNormalizer.Normalize(identifier);

        // Assert
        Assert.Equal("chrome.exe", result);
    }

    [Fact]
    public void Normalize_WithoutType_AddsExeForPathLikeIdentifiers()
    {
        // Arrange
        var identifier = "C:/Program Files/app";

        // Act
        var result = IdentifierNormalizer.Normalize(identifier);

        // Assert
        Assert.Equal("c:/program files/app.exe", result);
    }

    // ========================================================================
    // Edge cases
    // ========================================================================

    [Fact]
    public void Normalize_WithEmpty_ReturnsEmpty()
    {
        // Arrange
        var identifier = "";

        // Act
        var result = IdentifierNormalizer.Normalize(identifier);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Normalize_WithWhitespace_ReturnsEmpty()
    {
        // Arrange
        var identifier = "   ";

        // Act
        var result = IdentifierNormalizer.Normalize(identifier);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Normalize_WithNull_ReturnsEmpty()
    {
        // Arrange
        string? identifier = null;

        // Act
        var result = IdentifierNormalizer.Normalize(identifier!);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    // ========================================================================
    // Type Inference
    // ========================================================================

    [Fact]
    public void InferType_WithExeExtension_ReturnsExe()
    {
        // Arrange
        var identifier = "chrome.exe";

        // Act
        var result = IdentifierNormalizer.InferType(identifier);

        // Assert
        Assert.Equal(AppIdentifierType.Exe, result);
    }

    [Fact]
    public void InferType_WithDomain_ReturnsDomain()
    {
        // Arrange
        var identifier = "github.com";

        // Act
        var result = IdentifierNormalizer.InferType(identifier);

        // Assert
        Assert.Equal(AppIdentifierType.Domain, result);
    }

    [Fact]
    public void InferType_WithBackslash_ReturnsExe()
    {
        // Arrange
        var identifier = "C:\\Program Files\\app";

        // Act
        var result = IdentifierNormalizer.InferType(identifier);

        // Assert
        Assert.Equal(AppIdentifierType.Exe, result);
    }

    [Fact]
    public void InferType_WithForwardSlash_ReturnsExe()
    {
        // Arrange
        var identifier = "C:/Program Files/app";

        // Act
        var result = IdentifierNormalizer.InferType(identifier);

        // Assert
        Assert.Equal(AppIdentifierType.Exe, result);
    }

    // ========================================================================
    // Bug Regression Tests - Issue: duplicate key on update
    // ========================================================================

    [Fact]
    public void Normalize_BugRegression_MyAppApp_ShouldBeConsistent()
    {
        // This test verifies the fix for the bug where:
        // - Domain Entity: NormalizeIdentifier("myapp.app", Exe) → "myapp.app.exe"
        // - Repository: NormalizeIdentifier("myapp.app") → "myapp.app" (WRONG!)
        // Causing duplicate key violation on update

        // Arrange
        var identifier = "myapp.app";
        var type = AppIdentifierType.Exe;

        // Act - Simulating creation (with type)
        var createdIdentifier = IdentifierNormalizer.Normalize(identifier, type);

        // Act - Simulating search (without type)
        var searchIdentifier = IdentifierNormalizer.Normalize(createdIdentifier);

        // Assert - Both should be the same
        Assert.Equal("myapp.app.exe", createdIdentifier);
        Assert.Equal("myapp.app.exe", searchIdentifier);
        Assert.Equal(createdIdentifier, searchIdentifier);
    }

    [Fact]
    public void Normalize_BugRegression_DevenvVshost_ShouldBeConsistent()
    {
        // Another edge case: devenv.vshost (Visual Studio hosting process)

        // Arrange
        var identifier = "devenv.vshost";
        var type = AppIdentifierType.Exe;

        // Act
        var createdIdentifier = IdentifierNormalizer.Normalize(identifier, type);
        var searchIdentifier = IdentifierNormalizer.Normalize(createdIdentifier);

        // Assert
        Assert.Equal("devenv.vshost.exe", createdIdentifier);
        Assert.Equal("devenv.vshost.exe", searchIdentifier);
        Assert.Equal(createdIdentifier, searchIdentifier);
    }

    [Fact]
    public void Normalize_BugRegression_Chrome_ShouldBeConsistent()
    {
        // Standard case: chrome

        // Arrange
        var identifier = "chrome";
        var type = AppIdentifierType.Exe;

        // Act
        var createdIdentifier = IdentifierNormalizer.Normalize(identifier, type);
        var searchIdentifier = IdentifierNormalizer.Normalize("chrome"); // Raw input from UI

        // Assert
        Assert.Equal("chrome.exe", createdIdentifier);
        Assert.Equal("chrome.exe", searchIdentifier);
        Assert.Equal(createdIdentifier, searchIdentifier);
    }
}
