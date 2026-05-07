namespace TimeTrack.Agent.Infrastructure.Providers.Windows;

/// <summary>
/// Normalizes a folder path resolved from Windows File Explorer.
/// Ensures the returned string is a real filesystem path and is marked as a directory (trailing backslash).
/// </summary>
public static class WindowsExplorerFolderPathNormalizer
{
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var trimmed = raw.Trim();

        // Reject virtual/non-filesystem locations like "shell:::{GUID}"
        if (!LooksLikeWindowsFileSystemPath(trimmed))
            return null;

        var normalized = trimmed.Replace('/', '\\');
        if (!normalized.EndsWith("\\", StringComparison.Ordinal))
            normalized += "\\";

        return normalized;
    }

    private static bool LooksLikeWindowsFileSystemPath(string value)
    {
        var v = value.TrimStart();

        // Explicitly reject known virtual prefixes
        if (v.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
            return false;
        if (v.StartsWith("::{", StringComparison.OrdinalIgnoreCase))
            return false;

        // UNC
        if (v.StartsWith("\\\\", StringComparison.Ordinal))
            return true;

        // Drive root
        if (v.Length >= 3 && char.IsLetter(v[0]) && v[1] == ':' && (v[2] == '\\' || v[2] == '/'))
            return true;

        return false;
    }
}

