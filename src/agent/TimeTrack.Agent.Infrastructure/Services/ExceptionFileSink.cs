using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TimeTrack.Agent.Contracts.Services;

namespace TimeTrack.Agent.Infrastructure.Services;

/// <summary>
/// Writes normalized exception reports to daily JSONL files under ProgramData and
/// deletes logs older than 7 days to limit disk usage.
/// Must never throw to the caller.
/// </summary>
public sealed class ExceptionFileSink
{
    private const int RetentionDays = 7;

    private readonly string _directory;
    private readonly Func<DateTime> _utcNow;
    private readonly JsonSerializerOptions _jsonOptions;

    private readonly object _cleanupLock = new();
    private DateTime _lastCleanupDateUtc = DateTime.MinValue;

    public ExceptionFileSink(string? directory = null, Func<DateTime>? utcNow = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "TimeTrack",
            "Logs",
            "exceptions");
        _utcNow = utcNow ?? (() => DateTime.UtcNow);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task WriteAsync(ExceptionReport report, CancellationToken cancellationToken = default)
    {
        try
        {
            EnsureCleanupOncePerDay();

            Directory.CreateDirectory(_directory);

            var filePath = GetLogFilePath(report.Component, _utcNow().Date);
            var jsonLine = JsonSerializer.Serialize(report, _jsonOptions);

            await using var stream = new FileStream(
                filePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite);

            await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            // Do not propagate cancellation: logging is best-effort and must not throw to callers.
            await writer.WriteLineAsync(jsonLine).ConfigureAwait(false);
            await writer.FlushAsync().ConfigureAwait(false);
        }
        catch
        {
            // Best-effort: never throw
        }
    }

    internal string GetLogFilePath(string component, DateTime utcDate)
        => Path.Combine(_directory, $"{component}.exceptions.{utcDate:yyyy-MM-dd}.jsonl");

    private void EnsureCleanupOncePerDay()
    {
        var today = _utcNow().Date;
        if (_lastCleanupDateUtc == today) return;

        lock (_cleanupLock)
        {
            if (_lastCleanupDateUtc == today) return;
            try
            {
                CleanupOldLogs(today);
            }
            catch
            {
                // ignore
            }

            _lastCleanupDateUtc = today;
        }
    }

    internal void CleanupOldLogs(DateTime utcTodayDate)
    {
        if (!Directory.Exists(_directory)) return;

        // Keep the most recent RetentionDays days (including today).
        // Example: RetentionDays=7 and today=May 13 keeps May 7..May 13 and deletes May 6 and older.
        var cutoff = DateOnly.FromDateTime(utcTodayDate).AddDays(-(RetentionDays - 1));
        foreach (var file in Directory.EnumerateFiles(_directory, "*.exceptions.*.jsonl", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var fileName = Path.GetFileName(file);
                var parsedDate = TryParseDateFromFileName(fileName);
                var fileDate = parsedDate ?? DateOnly.FromDateTime(File.GetLastWriteTimeUtc(file));

                if (fileDate < cutoff)
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // ignore per-file
            }
        }
    }

    private static DateOnly? TryParseDateFromFileName(string fileName)
    {
        // Expected: "<component>.exceptions.YYYY-MM-DD.jsonl"
        // Be defensive: locate ".exceptions." and parse following 10 chars.
        var marker = ".exceptions.";
        var idx = fileName.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;

        var start = idx + marker.Length;
        if (start + 10 > fileName.Length) return null;

        var datePart = fileName.Substring(start, 10);
        if (DateOnly.TryParseExact(datePart, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var dt))
        {
            return dt;
        }
        return null;
    }
}
