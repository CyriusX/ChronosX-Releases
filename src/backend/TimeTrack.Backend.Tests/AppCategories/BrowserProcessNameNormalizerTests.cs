using FluentAssertions;
using TimeTrack.Backend.Domain.ValueObjects;
using Xunit;

namespace TimeTrack.Backend.Tests.AppCategories;

public sealed class BrowserProcessNameNormalizerTests
{
    [Theory]
    [InlineData("Chrome - WhatsApp (55)", "Chrome - WhatsApp")]
    [InlineData("Chrome - WhatsApp(30)", "Chrome - WhatsApp")]
    [InlineData("Chrome - ● WhatsApp (99+)", "Chrome - WhatsApp")]
    [InlineData("Chrome - Formula 1 (2026)", "Chrome - Formula 1 (2026)")]
    public void Normalize_ShouldCanonicalizeBrowserProcessNames(string raw, string expected)
    {
        BrowserProcessNameNormalizer.Normalize(raw).Should().Be(expected);
    }

    [Fact]
    public void Normalize_ShouldNotChangeNonBrowserProcessNames()
    {
        BrowserProcessNameNormalizer.Normalize("Slack - WhatsApp (55)")
            .Should().Be("Slack - WhatsApp (55)");
    }
}

