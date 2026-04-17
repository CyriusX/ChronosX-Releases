using FluentAssertions;
using TimeTrack.Agent.Infrastructure.Utilities;
using Xunit;

namespace TimeTrack.Agent.Tests.Infrastructure;

public sealed class FolderKeyNormalizerTests
{
    [Theory]
    [InlineData("/Users/junior/Documents/file.txt", "/Users/junior/Documents")]
    [InlineData("C:\\Users\\Junior\\Desktop\\notes.txt", "C:\\Users\\Junior\\Desktop")]
    [InlineData("\\\\server\\share\\dir\\file.txt", "\\\\server\\share\\dir")]
    [InlineData("file:///Users/junior/Downloads/file.txt", "/Users/junior/Downloads")]
    [InlineData("https://drive.google.com/drive/folders/abc?foo=bar#frag", "https://drive.google.com/drive/folders/abc?foo=bar")]
    [InlineData("https://onedrive.live.com/?id=abc#frag", "https://onedrive.live.com/?id=abc")]
    [InlineData("https://contoso.sharepoint.com/sites/a/Shared%20Documents/Folder", "https://contoso.sharepoint.com/sites/a/Shared%20Documents/Folder")]
    [InlineData("https://www.icloud.com/iclouddrive/0abc#frag", "https://www.icloud.com/iclouddrive/0abc")]
    public void Normalize_ShouldReturnExpectedKey(string raw, string expected)
    {
        FolderKeyNormalizer.Normalize(raw).Should().Be(expected);
    }

    [Theory]
    [InlineData("https://youtube.com/watch?v=123")]
    [InlineData("relative/path/to/file.txt")]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_ShouldRejectDisallowedInputs(string raw)
    {
        FolderKeyNormalizer.Normalize(raw).Should().BeNull();
    }
}

