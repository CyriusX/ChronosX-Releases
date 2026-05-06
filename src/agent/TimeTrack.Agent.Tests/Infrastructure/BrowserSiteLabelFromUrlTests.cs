using FluentAssertions;
using TimeTrack.Agent.Infrastructure.Utilities;
using Xunit;

namespace TimeTrack.Agent.Tests.Infrastructure;

public sealed class BrowserSiteLabelFromUrlTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=abc", "YouTube")]
    [InlineData("https://music.youtube.com/watch?v=abc", "YouTube")]
    [InlineData("https://web.whatsapp.com/", "WhatsApp")]
    [InlineData("https://web.telegram.org/k/", "Telegram")]
    [InlineData("https://github.com/openai", "GitHub")]
    [InlineData("https://accounts.google.com/", "Google")]
    [InlineData("https://openai.com/blog/hello", "openai.com")]
    [InlineData("https://subdomain.openai.com/blog/hello", "openai.com")]
    public void FromUrl_ShouldReturnStableLabel(string rawUrl, string expected)
    {
        BrowserSiteLabelFromUrl.FromUrl(rawUrl).Should().Be(expected);
    }

    [Theory]
    [InlineData("YouTube", true)]
    [InlineData("openai.com", true)]
    [InlineData("Inbox", false)]
    [InlineData("How to build an app in React", false)]
    public void IsLikelyStableSiteLabel_ShouldMatchExpected(string candidate, bool expected)
    {
        BrowserSiteLabelFromUrl.IsLikelyStableSiteLabel(candidate).Should().Be(expected);
    }
}

