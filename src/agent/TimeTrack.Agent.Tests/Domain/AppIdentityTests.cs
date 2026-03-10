using FluentAssertions;
using Xunit;
using TimeTrack.Agent.Domain.Common;
using TimeTrack.Agent.Domain.Entities;
using TimeTrack.Agent.Domain.ValueObjects;

namespace TimeTrack.Agent.Tests.Domain;

public class AppIdentityTests
{
    [Fact]
    public void Constructor_WithValidData_ShouldCreateAppIdentity()
    {
        // Arrange
        const string exePathHash = "abc123def456";
        const string displayName = "Visual Studio Code";

        // Act
        var app = new AppIdentity(exePathHash, displayName);

        // Assert
        app.ExePathHash.Should().Be(exePathHash);
        app.DisplayName.Should().Be(displayName);
        app.Category.Should().Be(AppCategory.Unknown);
    }

    [Fact]
    public void Constructor_WithCategory_ShouldSetCategory()
    {
        // Arrange
        var category = AppCategory.Productive("development", "org_override");

        // Act
        var app = new AppIdentity("hash123", "VS Code", category);

        // Assert
        app.Category.Should().Be(category);
        app.Category.IsProductive.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithNullExePathHash_ShouldThrowDomainException()
    {
        // Act
        var act = () => new AppIdentity(null!, "Display Name");

        // Assert
        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "REQUIRED_FIELD");
    }

    [Fact]
    public void Constructor_WithEmptyExePathHash_ShouldThrowDomainException()
    {
        // Act
        var act = () => new AppIdentity("", "Display Name");

        // Assert
        act.Should().Throw<DomainException>()
            .Where(e => e.Code == "REQUIRED_FIELD");
    }

    [Fact]
    public void Constructor_WithNullDisplayName_ShouldSetEmptyString()
    {
        // Act
        var app = new AppIdentity("hash123", null!);

        // Assert
        app.DisplayName.Should().BeEmpty();
    }

    [Fact]
    public void UpdateCategory_ShouldUpdateCategory()
    {
        // Arrange
        var app = new AppIdentity("hash123", "VS Code");
        var newCategory = AppCategory.Productive("development", "global");

        // Act
        app.UpdateCategory(newCategory);

        // Assert
        app.Category.Should().Be(newCategory);
    }

    [Fact]
    public void UpdateCategory_WithNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        var app = new AppIdentity("hash123", "VS Code");

        // Act
        var act = () => app.UpdateCategory(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Equality_WithDifferentIds_ShouldNotBeEqual()
    {
        // Arrange
        var app1 = new AppIdentity("hash1", "App 1");
        var app2 = new AppIdentity("hash1", "App 1");

        // Assert - entidades diferentes têm IDs diferentes
        app1.Should().NotBe(app2);
        app1.Id.Should().NotBe(app2.Id);
    }
}
