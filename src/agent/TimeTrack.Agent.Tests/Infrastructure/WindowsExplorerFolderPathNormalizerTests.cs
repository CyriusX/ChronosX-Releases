using FluentAssertions;
using TimeTrack.Agent.Infrastructure.Providers.Windows;
using Xunit;

namespace TimeTrack.Agent.Tests.Infrastructure;

public sealed class WindowsExplorerFolderPathNormalizerTests
{
    [Theory]
    [InlineData(@"C:\Users\Me\Downloads", @"C:\Users\Me\Downloads\")]
    [InlineData(@"C:/Users/Me/Downloads", @"C:\Users\Me\Downloads\")]
    [InlineData(@"\\server\share\dir", @"\\server\share\dir\")]
    public void Normalize_ShouldMarkAsDirectory_WhenFileSystemPath(string raw, string expected)
    {
        WindowsExplorerFolderPathNormalizer.Normalize(raw).Should().Be(expected);
    }

    [Theory]
    [InlineData("shell:::{20D04FE0-3AEA-1069-A2D8-08002B30309D}")]
    [InlineData("::{20D04FE0-3AEA-1069-A2D8-08002B30309D}")]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_ShouldReturnNull_WhenNotARealFolderPath(string raw)
    {
        WindowsExplorerFolderPathNormalizer.Normalize(raw).Should().BeNull();
    }
}

