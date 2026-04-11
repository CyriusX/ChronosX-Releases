using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TimeTrack.Agent.Infrastructure.MacOS.Providers;
using Xunit;

namespace TimeTrack.Agent.Tests.MacOS.Providers;

public class MacOSFilePathExtractorTests
{
    private readonly MacOSFilePathExtractor _extractor;

    public MacOSFilePathExtractorTests()
    {
        _extractor = new MacOSFilePathExtractor();
    }

    [Theory]
    [InlineData("Finder", "Documents — john", "/Users/john/Documents")]
    [InlineData("Finder", "Projects", null)]
    public void ExtractFilePath_Should_HandleFinderTitles(string appName, string windowTitle, string? expectedPath)
    {
        var result = _extractor.ExtractFilePath(IntPtr.Zero, appName, windowTitle);

        if (expectedPath != null)
        {
            result.Should().Be(expectedPath);
        }
    }

    [Theory]
    [InlineData("Code", "main.cs - TimeTrack - Visual Studio Code", "main.cs")]
    public void ExtractFilePath_Should_HandleVSCodeTitles(string appName, string windowTitle, string expectedFileName)
    {
        var result = _extractor.ExtractFilePath(IntPtr.Zero, appName, windowTitle);

        result.Should().Contain(expectedFileName);
    }

    [Theory]
    [InlineData("Safari", "Google — Safari", null)]
    [InlineData("Terminal", "bash — 80×24", null)]
    public void ExtractFilePath_Should_ReturnNull_ForNonFileTitles(string appName, string windowTitle, string? expectedPath)
    {
        var result = _extractor.ExtractFilePath(IntPtr.Zero, appName, windowTitle);

        result.Should().Be(expectedPath);
    }

    [Fact]
    public void ExtractFilePath_Should_HandleNullWindowTitle()
    {
        var result = _extractor.ExtractFilePath(IntPtr.Zero, "Finder", null);

        result.Should().BeNull();
    }
}
