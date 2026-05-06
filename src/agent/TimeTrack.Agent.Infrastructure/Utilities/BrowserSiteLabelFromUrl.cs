namespace TimeTrack.Agent.Infrastructure.Utilities;

/// <summary>
/// Converts a browser tab URL into a stable "site/app label" suitable for browser app keys.
/// Examples:
/// - https://www.youtube.com/watch?v=... -> "YouTube"
/// - https://web.whatsapp.com/ -> "WhatsApp"
/// - https://openai.com/blog/... -> "openai.com"
/// </summary>
public static class BrowserSiteLabelFromUrl
{
    private static readonly HashSet<string> KnownStableLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "YouTube",
        "Google",
        "WhatsApp",
        "Telegram",
        "GitHub"
    };

    private static readonly HashSet<string> CommonSecondLevelDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "co", "com", "net", "org", "gov", "edu"
    };

    public static string? FromUrl(string rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
            return null;

        if (!Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out var uri))
            return null;

        if (string.IsNullOrWhiteSpace(uri.Host))
            return null;

        var host = uri.Host.Trim().ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal))
            host = host[4..];

        // Friendly mappings (suffix-based)
        if (host == "youtube.com" || host.EndsWith(".youtube.com", StringComparison.Ordinal))
            return "YouTube";

        // Covers accounts.google.com, mail.google.com, google.ca, etc.
        if (host == "google.com" ||
            host.EndsWith(".google.com", StringComparison.Ordinal) ||
            host.StartsWith("google.", StringComparison.Ordinal) ||
            host.Contains(".google.", StringComparison.Ordinal))
            return "Google";

        if (host == "github.com" || host.EndsWith(".github.com", StringComparison.Ordinal))
            return "GitHub";

        if (host == "whatsapp.com" || host.EndsWith(".whatsapp.com", StringComparison.Ordinal))
            return "WhatsApp";

        if (host == "telegram.org" || host.EndsWith(".telegram.org", StringComparison.Ordinal))
            return "Telegram";

        // Default: prefer a short/registrable-ish host when safe; otherwise return the full host.
        return GetBaseDomain(host) ?? host;
    }

    public static bool IsLikelyStableSiteLabel(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        var c = candidate.Trim();

        if (KnownStableLabels.Contains(c))
            return true;

        // Domain-like labels are stable enough for app keys.
        if (c.Contains('.', StringComparison.Ordinal) && c.Length <= 80 && !c.Contains('/', StringComparison.Ordinal))
            return true;

        // Reject long/space-heavy values — likely the full tab title (not stable).
        if (c.Length > 48)
            return false;

        if (c.Contains(' ', StringComparison.Ordinal) && !c.Contains('.', StringComparison.Ordinal))
            return false;

        return false;
    }

    private static string? GetBaseDomain(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return null;

        var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length <= 2)
            return host;

        var tld = parts[^1];
        var sld = parts[^2];

        // Heuristic for ccTLDs where a common second-level domain is used (e.g., .co.uk, .com.au).
        if (tld.Length == 2 && parts.Length >= 3 && CommonSecondLevelDomains.Contains(sld))
        {
            return string.Join('.', parts[^3], sld, tld);
        }

        return string.Join('.', sld, tld);
    }
}

