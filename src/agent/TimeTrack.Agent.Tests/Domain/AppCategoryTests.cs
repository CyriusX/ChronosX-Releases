using FluentAssertions;
using Xunit;
using TimeTrack.Agent.Domain.ValueObjects;

namespace TimeTrack.Agent.Tests.Domain;

public class AppCategoryTests
{
    [Fact]
    public void Unknown_ShouldReturnDefaultCategory()
    {
        // Arrange & Act
        var unknown = AppCategory.Unknown;

        // Assert
        unknown.Productivity.Should().Be("neutral");
        unknown.Subcategory.Should().Be("unknown");
        unknown.Source.Should().Be("default");
    }

    [Fact]
    public void Productive_ShouldCreateProductiveCategory()
    {
        // Arrange & Act
        var category = AppCategory.Productive("development", "global");

        // Assert
        category.Productivity.Should().Be("productive");
        category.Subcategory.Should().Be("development");
        category.Source.Should().Be("global");
        category.IsProductive.Should().BeTrue();
    }

    [Fact]
    public void Distraction_ShouldCreateDistractionCategory()
    {
        // Arrange & Act
        var category = AppCategory.Distraction("social_media");

        // Assert
        category.Productivity.Should().Be("distraction");
        category.Subcategory.Should().Be("social_media");
        category.Source.Should().Be("global");
        category.IsDistraction.Should().BeTrue();
    }

    [Fact]
    public void Neutral_ShouldCreateNeutralCategory()
    {
        // Arrange & Act
        var category = AppCategory.Neutral("utilities");

        // Assert
        category.Productivity.Should().Be("neutral");
        category.Subcategory.Should().Be("utilities");
        category.IsNeutral.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithNullProductivity_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new AppCategory(null!, "subcategory", "source");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Equality_WithSameValues_ShouldBeEqual()
    {
        // Arrange
        var category1 = AppCategory.Productive("development", "global");
        var category2 = AppCategory.Productive("development", "global");

        // Assert
        category1.Should().Be(category2);
        category1.GetHashCode().Should().Be(category2.GetHashCode());
    }

    [Fact]
    public void Equality_WithDifferentValues_ShouldNotBeEqual()
    {
        // Arrange
        var category1 = AppCategory.Productive("development", "global");
        var category2 = AppCategory.Productive("development", "org_override");

        // Assert
        category1.Should().NotBe(category2);
    }
}
