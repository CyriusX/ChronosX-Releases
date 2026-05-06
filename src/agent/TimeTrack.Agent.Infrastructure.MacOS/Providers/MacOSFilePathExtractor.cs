using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Providers;

namespace TimeTrack.Agent.Infrastructure.MacOS.Providers;

/// <summary>
/// Extracts file paths from active windows on macOS
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacOSFilePathExtractor : IFilePathExtractor
{
    private readonly ILogger<MacOSFilePathExtractor> _logger;

    // Browsers are tracked via Domain/BrowserUrl; do not put titles into FilePath.
    private static readonly HashSet<string> BrowserProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "safari",
        "google chrome", "chrome", "chromium",
        "firefox",
        "microsoft edge", "edge",
        "brave browser", "brave",
        "opera",
        "arc"
    };

    private static readonly HashSet<string> AppsWithPathExtraction = new(StringComparer.OrdinalIgnoreCase)
    {
        "code", "vscode",
        "textmate", "coteditor",
        "sublime text", "sublime_text",
        "atom",
        "xcode",
        "intellij idea", "idea",
        "webstorm",
        "rider",
        "pycharm",
        "clion",
        "obsidian",
        "typora",
        "macvim", "vimr",
        "bbedit", "textwrangler"
    };

    public MacOSFilePathExtractor(ILogger<MacOSFilePathExtractor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private static bool LooksLikeAbsolutePath(string? value)
        => !string.IsNullOrWhiteSpace(value) && value.TrimStart().StartsWith("/", StringComparison.Ordinal);

    private static bool IsBrowser(string processLower)
        => BrowserProcesses.Contains(processLower);

    /// <inheritdoc />
    public string? ExtractFilePath(IntPtr windowHandle, string processName, string? windowTitle)
    {
        if (string.IsNullOrEmpty(processName))
            return null;

        try
        {
            var processLower = processName.ToLowerInvariant();

            if (IsBrowser(processLower))
                return null;

            if (processLower.Contains("finder"))
            {
                return ExtractFinderPath(windowTitle);
            }

            if (processLower.Contains("code") || processLower == "code" || processLower == "code64")
            {
                return ExtractVsCodePath(windowTitle);
            }

            if (IsJetBrainsIde(processLower))
            {
                return ExtractJetBrainsPath(windowTitle);
            }

            if (AppsWithPathExtraction.Contains(processLower))
                return ExtractFromGenericTitle(windowTitle, processName);

            // Unknown apps: only accept when it already looks like an absolute path.
            return ExtractFromGenericTitle(windowTitle, processName);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error extracting filepath for {Process}", processName);
            return null;
        }
    }

    private string? ExtractFinderPath(string? windowTitle)
    {
        if (string.IsNullOrEmpty(windowTitle))
            return null;

        // Prefer AXDocument (handled by MacOSActiveWindowProvider). If we only have a title,
        // return it only when it's already an absolute POSIX path.
        return LooksLikeAbsolutePath(windowTitle) ? windowTitle.Trim() : null;
    }

    private string? ExtractVsCodePath(string? windowTitle)
    {
        if (string.IsNullOrEmpty(windowTitle))
            return null;

        var title = windowTitle;
        var suffixes = new[] { " - Visual Studio Code", " - VS Code", " — Visual Studio Code", " — VS Code" };

        foreach (var suffix in suffixes)
        {
            var idx = title.LastIndexOf(suffix, StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
            {
                title = title[..idx];
                break;
            }
        }

        // VS Code titles often contain only "file - workspace". Only return when the title
        // itself already contains an absolute path.
        return LooksLikeAbsolutePath(title) ? title.Trim() : null;
    }

    private string? ExtractJetBrainsPath(string? windowTitle)
    {
        if (string.IsNullOrEmpty(windowTitle))
            return null;

        var separators = new[] { " – ", " — ", " - " };

        foreach (var sep in separators)
        {
            var parts = windowTitle.Split(new[] { sep }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                var file = parts[0].Trim();

                // Some JetBrains windows can expose the absolute path as the first segment.
                return LooksLikeAbsolutePath(file) ? file : null;
            }
        }

        return LooksLikeAbsolutePath(windowTitle) ? windowTitle.Trim() : null;
    }

    private string? ExtractFromGenericTitle(string? windowTitle, string processName)
    {
        if (string.IsNullOrEmpty(windowTitle))
            return null;

        var title = windowTitle;
        var suffixes = new[] { " - Google Chrome", " - Mozilla Firefox", " - Microsoft Edge",
                               " - Brave", " - Opera", " - Safari",
                               " — Google Chrome", " — Mozilla Firefox", " — Microsoft Edge",
                               " — Brave", " — Opera", " — Safari" };

        foreach (var suffix in suffixes)
        {
            var idx = title.LastIndexOf(suffix, StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
            {
                title = title[..idx];
                break;
            }
        }

        // Only accept absolute paths; otherwise we'd store plain titles like "Google".
        var trimmed = title.Trim();
        return LooksLikeAbsolutePath(trimmed) ? trimmed : null;
    }

    private static bool IsJetBrainsIde(string processName)
    {
        return processName.Contains("idea") ||
               processName.Contains("webstorm") ||
               processName.Contains("rider") ||
               processName.Contains("pycharm") ||
               processName.Contains("clion") ||
               processName.Contains("goland") ||
               processName.Contains("appcode") ||
               processName.Contains("datagrip") ||
               processName.Contains("rubymine");
    }
}
