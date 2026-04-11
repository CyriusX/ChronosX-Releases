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

    /// <inheritdoc />
    public string? ExtractFilePath(IntPtr windowHandle, string processName, string? windowTitle)
    {
        if (string.IsNullOrEmpty(processName))
            return null;

        try
        {
            var processLower = processName.ToLowerInvariant();

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
            {
                return ExtractFromGenericTitle(windowTitle, processName);
            }

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

        if (windowTitle.StartsWith("/"))
            return windowTitle;

        return windowTitle.Trim();
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

        var parts = title.Split(new[] { " - ", " — " }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            return $"{parts[1].Trim()}/{parts[0].Trim()}";
        }

        if (parts.Length == 1)
        {
            return parts[0].Trim();
        }

        return title.Trim();
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
                var project = parts[^1].Trim();
                var file = parts[0].Trim();

                if (project.Contains("IDEA") || project.Contains("WebStorm") ||
                    project.Contains("Rider") || project.Contains("PyCharm") ||
                    project.Contains("CLion"))
                {
                    if (parts.Length >= 3)
                    {
                        return $"{parts[^2].Trim()}/{file}";
                    }
                    return file;
                }

                return $"{project}/{file}";
            }
        }

        return windowTitle;
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

        return title.Trim();
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
