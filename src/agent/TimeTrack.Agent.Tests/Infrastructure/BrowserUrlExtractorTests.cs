using FluentAssertions;
using TimeTrack.Agent.Infrastructure.Providers.Windows;
using Xunit;

namespace TimeTrack.Agent.Tests.Infrastructure;

public sealed class BrowserUrlExtractorTests
{
    [Theory]
    [InlineData("(55) WhatsApp Web", "WhatsApp Web")]
    [InlineData("WhatsApp Web (55)", "WhatsApp Web")]
    [InlineData("WhatsApp Web(30)", "WhatsApp Web")]
    [InlineData("● WhatsApp Web (99+)", "WhatsApp Web")]
    [InlineData("Formula 1 (2026)", "Formula 1 (2026)")]
    public void NormalizeSiteName_ShouldRemoveDynamicBadgeCounts(string raw, string expected)
    {
        BrowserUrlExtractor.NormalizeSiteName(raw).Should().Be(expected);
    }
}

