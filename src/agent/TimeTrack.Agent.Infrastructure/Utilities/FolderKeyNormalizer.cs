namespace TimeTrack.Agent.Infrastructure.Utilities;

/// <summary>
/// Normalizes a "folder key" for the Top Folders feature.
/// - Accepts only real file-system folders (POSIX absolute, Windows drive/UNC, file://)
/// - Also accepts *only* cloud-drive URLs (Google Drive / OneDrive / iCloud Drive)
/// </summary>
public static class FolderKeyNormalizer
{
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var trimmed = raw.Trim();

        // 1) Cloud-drive URLs (allowed subset only)
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
                return null;

            if (!IsAllowedCloudDriveHost(uri.Host))
                return null;

            var host = uri.Host.ToLowerInvariant();
            var path = uri.AbsolutePath;
            while (path.Length > 1 && path.EndsWith("/", StringComparison.Ordinal))
                path = path[..^1];

            // Stable key = scheme://host + path + query (drop fragments)
            return $"{uri.Scheme.ToLowerInvariant()}://{host}{path}{uri.Query}";
        }

        var pathValue = trimmed;
        var hadTrailingSep = pathValue.EndsWith("\\", StringComparison.Ordinal) || pathValue.EndsWith("/", StringComparison.Ordinal);

        if (pathValue.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(pathValue);
                pathValue = uri.LocalPath;
                hadTrailingSep = trimmed.EndsWith("/", StringComparison.Ordinal) || trimmed.EndsWith("\\", StringComparison.Ordinal);
            }
            catch
            {
                // Ignore and fall back.
            }
        }

        if (!LooksLikeFileSystemPath(pathValue))
            return null;

        // Trim trailing separators, keeping roots intact.
        while (pathValue.Length > 1 && (pathValue.EndsWith("\\", StringComparison.Ordinal) || pathValue.EndsWith("/", StringComparison.Ordinal)))
        {
            if (pathValue == "/")
                break;
            if (pathValue.Length == 3 && char.IsLetter(pathValue[0]) && pathValue[1] == ':' && (pathValue[2] == '\\' || pathValue[2] == '/'))
                break;
            pathValue = pathValue[..^1];
        }

        if (hadTrailingSep)
            return pathValue;

        var lastSlash = pathValue.LastIndexOf('/');
        var lastBackslash = pathValue.LastIndexOf('\\');
        var lastSep = Math.Max(lastSlash, lastBackslash);

        if (lastSep < 0)
            return pathValue;

        if (lastSep == 2 && pathValue.Length >= 3 && char.IsLetter(pathValue[0]) && pathValue[1] == ':' && (pathValue[2] == '\\' || pathValue[2] == '/'))
            return pathValue[..3].Replace('/', '\\');

        if (lastSep == 0)
            return pathValue[..1];

        return pathValue[..lastSep];
    }

    private static bool LooksLikeFileSystemPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var p = path.TrimStart();

        if (p.StartsWith("/", StringComparison.Ordinal))
            return true;

        if (p.StartsWith("\\\\", StringComparison.Ordinal))
            return true;

        if (p.Length >= 3 &&
            char.IsLetter(p[0]) &&
            p[1] == ':' &&
            (p[2] == '\\' || p[2] == '/'))
            return true;

        return false;
    }

    private static bool IsAllowedCloudDriveHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;

        var h = host.Trim().ToLowerInvariant();

        if (h == "drive.google.com")
            return true;

        if (h == "onedrive.live.com" || h == "1drv.ms")
            return true;

        if (h.EndsWith(".sharepoint.com", StringComparison.Ordinal) || h.EndsWith(".my.sharepoint.com", StringComparison.Ordinal))
            return true;

        if (h == "icloud.com" || h == "www.icloud.com")
            return true;

        return false;
    }
}

