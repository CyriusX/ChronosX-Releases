using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Providers;

namespace TimeTrack.Agent.Infrastructure.Providers.Windows;

/// <summary>
/// Extracts the site/domain from a browser window title.
///
/// Browser window titles follow the pattern: "Page Title - Site Name - Browser Name"
/// After stripping the browser suffix, the last segment is usually the site/service name.
///
/// Examples:
///   "John - Telegram Web - Google Chrome"  → site = "Telegram Web"
///   "Issues · my-repo · GitHub - Google Chrome"  → site = "GitHub"
///   "Funny Cat Video - YouTube - Google Chrome"  → site = "YouTube"
///   "New Tab - Google Chrome"  → site = "New Tab" (generic, ignored)
/// </summary>
public sealed class BrowserUrlExtractor : IBrowserUrlExtractor
{
    private readonly ILogger<BrowserUrlExtractor> _logger;

    // Known browser exe names for which URL extraction is attempted
    private static readonly HashSet<string> BrowserExeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "firefox", "msedge", "opera", "brave", "safari", "arc",
        "vivaldi", "waterfox", "chromium", "librewolf", "iexplore"
    };

    public BrowserUrlExtractor(ILogger<BrowserUrlExtractor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Check if an exe path belongs to a known browser.
    /// </summary>
    public static bool IsBrowserExe(string? exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath)) return false;
        var exeName = Path.GetFileNameWithoutExtension(exePath);
        return BrowserExeNames.Contains(exeName);
    }

    /// <inheritdoc />
    public string? TryExtractUrl(IntPtr windowHandle)
    {
        // Window-title-based extraction doesn't use the handle
        return null;
    }

    /// <summary>
    /// Extracts the site name from a browser window title by stripping
    /// the browser suffix and taking the last meaningful segment.
    /// Returns a pseudo-domain that can be matched by BrowserTabCategorizer.
    /// </summary>
    public static string? ExtractSiteFromTitle(string? windowTitle, string? browserName)
    {
        if (string.IsNullOrWhiteSpace(windowTitle))
            return null;

        // Step 1: Strip browser suffix — "Page - Site - Google Chrome" → "Page - Site"
        var stripped = windowTitle;
        var separators = new[] { " - ", " — ", " – " };

        foreach (var sep in separators)
        {
            var lastIdx = stripped.LastIndexOf(sep, StringComparison.Ordinal);
            if (lastIdx > 0)
            {
                var suffix = stripped[(lastIdx + sep.Length)..].Trim();
                // If the suffix matches the browser name, strip it
                if (!string.IsNullOrWhiteSpace(browserName) &&
                    suffix.Contains(browserName, StringComparison.OrdinalIgnoreCase))
                {
                    stripped = stripped[..lastIdx].Trim();
                    break;
                }
                // Common browser suffixes
                if (IsBrowserName(suffix))
                {
                    stripped = stripped[..lastIdx].Trim();
                    break;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(stripped))
            return null;

        // Step 2: Take the last segment as the site name
        // "Page Title - Site Name" → "Site Name"
        // "Issues · my-repo · GitHub" → "GitHub"
        var segmentSeps = new[] { " - ", " — ", " – ", " · ", " | " };

        foreach (var sep in segmentSeps)
        {
            var lastIdx = stripped.LastIndexOf(sep, StringComparison.Ordinal);
            if (lastIdx > 0)
            {
                var site = stripped[(lastIdx + sep.Length)..].Trim();
                if (site.Length > 1 && !IsGenericTitle(site))
                    return site;
            }
        }

        // No segments — the entire stripped title is the site name (e.g., "YouTube")
        if (!IsGenericTitle(stripped))
            return stripped;

        return null;
    }

    private static readonly HashSet<string> KnownBrowserNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Google Chrome", "Mozilla Firefox", "Microsoft Edge", "Opera", "Brave",
        "Safari", "Arc", "Vivaldi", "Waterfox", "Chromium", "LibreWolf",
        "Internet Explorer"
    };

    private static bool IsBrowserName(string name) =>
        KnownBrowserNames.Contains(name);

    private static bool IsGenericTitle(string title)
    {
        var lower = title.ToLowerInvariant();
        return lower is "new tab" or "nova guia" or "nova aba"
            or "about:blank" or "start page" or "home"
            or "untitled" or "sem título";
    }
}
