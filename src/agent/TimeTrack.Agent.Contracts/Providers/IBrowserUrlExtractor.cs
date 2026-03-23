namespace TimeTrack.Agent.Contracts.Providers;

/// <summary>
/// Extracts the current URL from a browser window via UI Automation.
/// Returns null for non-browser windows or when extraction fails.
/// </summary>
public interface IBrowserUrlExtractor
{
    /// <summary>
    /// Attempts to extract the URL from the browser's address bar.
    /// </summary>
    /// <param name="windowHandle">Native window handle (HWND)</param>
    /// <returns>The URL string, or null if extraction fails</returns>
    string? TryExtractUrl(IntPtr windowHandle);

    /// <summary>
    /// Extracts the domain (host) from a full URL.
    /// Ex: "https://www.github.com/user/repo" → "github.com"
    /// </summary>
    static string? ExtractDomain(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        try
        {
            // Handle URLs without scheme
            var normalized = url.Contains("://") ? url : "https://" + url;
            var uri = new Uri(normalized);
            var host = uri.Host;

            // Strip "www." prefix
            if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                host = host[4..];

            return host;
        }
        catch
        {
            return null;
        }
    }
}
