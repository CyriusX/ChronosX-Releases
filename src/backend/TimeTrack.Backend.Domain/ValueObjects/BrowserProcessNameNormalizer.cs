using System.Text.RegularExpressions;

namespace TimeTrack.Backend.Domain.ValueObjects;

/// <summary>
/// Normalizes browser-derived "process names" that include dynamic badge counts.
///
/// The Agent sends browser activity as a synthetic process name:
///   "{BrowserName} - {Site/App Name}"
///
/// Many sites append unread counts in the tab title, which can cause duplicates:
///   "Chrome - WhatsApp (55)" vs "Chrome - WhatsApp (30)"
///
/// This normalizer removes those dynamic counts so the same site always maps to a single key.
/// </summary>
public static class BrowserProcessNameNormalizer
{
    private static readonly HashSet<string> KnownBrowserDisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Google Chrome", "Chrome",
        "Microsoft Edge", "Edge",
        "Mozilla Firefox", "Firefox",
        "Brave", "Opera", "Safari", "Arc",
        "Vivaldi", "Waterfox", "Chromium", "LibreWolf",
        "Internet Explorer"
    };

    /// <summary>
    /// Normalizes a process name. If it matches "{Browser} - {Site}", strips dynamic badge counts
    /// from the site portion and returns a stable key.
    /// </summary>
    public static string Normalize(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
            return processName;

        var trimmed = processName.Trim();
        const string sep = " - ";
        var idx = trimmed.IndexOf(sep, StringComparison.Ordinal);
        if (idx <= 0)
            return trimmed;

        var browser = trimmed[..idx].Trim();
        if (!KnownBrowserDisplayNames.Contains(browser))
            return trimmed;

        var site = trimmed[(idx + sep.Length)..].Trim();
        if (string.IsNullOrWhiteSpace(site))
            return trimmed;

        var normalizedSite = NormalizeSiteName(site);
        if (string.IsNullOrWhiteSpace(normalizedSite))
            return trimmed;

        return $"{browser} - {normalizedSite}";
    }

    private static string NormalizeSiteName(string site)
    {
        if (string.IsNullOrWhiteSpace(site))
            return site;

        // Strip notification/badge counts (common in browser tabs).
        // Leading: "(55) WhatsApp Web" / "● (55) WhatsApp Web" → "WhatsApp Web"
        // Trailing: "WhatsApp Web (55)" / "WhatsApp Web(30)" / "Slack (99+)" → "WhatsApp Web" / "Slack"
        //
        // Only strips 1–3 digit counts (optionally with '+') to avoid removing years like "(2026)".
        var result = Regex.Replace(site, @"^(?:[•●]\s*)?\((\d{1,3}\+?)\)\s*", string.Empty).Trim();
        result = Regex.Replace(result, @"\s*\((\d{1,3}\+?)\)\s*$", string.Empty).Trim();
        result = Regex.Replace(result, @"^[•●]\s*", string.Empty).Trim();
        result = Regex.Replace(result, @"\s{2,}", " ").Trim();
        return result;
    }
}
