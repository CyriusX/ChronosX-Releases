using System.Text.Json;
using FluentAssertions;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Infrastructure.Services;
using Xunit;

namespace TimeTrack.Agent.Tests.Infrastructure;

public sealed class ExceptionFileSinkTests
{
    [Fact]
    public async Task WriteAsync_ShouldAppendJsonLine_WhenCalled()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "timetrack-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var now = new DateTime(2026, 5, 13, 12, 0, 0, DateTimeKind.Utc);
            var sink = new ExceptionFileSink(directory: tempDir, utcNow: () => now);

            var ex = new InvalidOperationException("boom");
            var report = ExceptionReport.FromException(ex, "AgentService", "ipc.command.Test");

            await sink.WriteAsync(report);

            var files = Directory.EnumerateFiles(tempDir, "*.jsonl").ToList();
            files.Should().HaveCount(1);
            Path.GetFileName(files[0]).Should().Be("AgentService.exceptions.2026-05-13.jsonl");

            var lines = await File.ReadAllLinesAsync(files[0]);
            lines.Should().HaveCount(1);

            using var doc = JsonDocument.Parse(lines[0]);
            doc.RootElement.GetProperty("component").GetString().Should().Be("AgentService");
            doc.RootElement.GetProperty("operation").GetString().Should().Be("ipc.command.Test");
            doc.RootElement.GetProperty("exceptionType").GetString().Should().Contain(nameof(InvalidOperationException));
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void CleanupOldLogs_ShouldDeleteFilesOlderThan7Days()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "timetrack-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var nowDate = new DateTime(2026, 5, 13, 0, 0, 0, DateTimeKind.Utc);
            var sink = new ExceptionFileSink(directory: tempDir, utcNow: () => nowDate);

            // Keep last 7 days including today (cutoff is 2026-05-07)
            File.WriteAllText(Path.Combine(tempDir, "AgentService.exceptions.2026-05-05.jsonl"), "{}\n");
            File.WriteAllText(Path.Combine(tempDir, "AgentService.exceptions.2026-05-06.jsonl"), "{}\n");
            // Exactly at cutoff: should be kept
            File.WriteAllText(Path.Combine(tempDir, "AgentService.exceptions.2026-05-07.jsonl"), "{}\n");
            // Newer: should be kept
            File.WriteAllText(Path.Combine(tempDir, "AgentService.exceptions.2026-05-13.jsonl"), "{}\n");

            sink.CleanupOldLogs(nowDate);

            Directory.EnumerateFiles(tempDir, "*.jsonl")
                .Select(Path.GetFileName)
                .Should()
                .BeEquivalentTo(new[]
                {
                    "AgentService.exceptions.2026-05-07.jsonl",
                    "AgentService.exceptions.2026-05-13.jsonl"
                });
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }
}
