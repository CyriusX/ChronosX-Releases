namespace TimeTrack.Backend.Domain.ValueObjects;

/// <summary>
/// Centralized identifier normalization for app categories
///
/// SRP: Single responsibility for normalizing app identifiers
/// DIP: Used by entities and repositories alike
///
/// This ensures consistent normalization across:
/// - Domain entities (AppCategoryOverride, AppCategoryGlobal)
/// - Repositories (FindAsync, GetByOrgAndIdentifiersAsync)
/// - Services (AppCategoryResolver)
/// </summary>
public static class IdentifierNormalizer
{
    /// <summary>
    /// Normalizes an app identifier based on its type
    ///
    /// Rules:
    /// - Trim whitespace
    /// - Convert to lowercase
    /// - For Exe type: ensure .exe suffix if not present
    /// - For Domain type: keep as-is (no suffix)
    /// </summary>
    /// <param name="identifier">The raw identifier (e.g., "Chrome", "github.com")</param>
    /// <param name="identifierType">The type of identifier (Exe or Domain)</param>
    /// <returns>Normalized identifier (e.g., "chrome.exe", "github.com")</returns>
    public static string Normalize(string identifier, AppIdentifierType identifierType)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return string.Empty;

        var normalized = identifier.Trim().ToLowerInvariant();

        if (identifierType == AppIdentifierType.Exe && !normalized.EndsWith(".exe"))
        {
            normalized += ".exe";
        }

        return normalized;
    }

    /// <summary>
    /// Normalizes an identifier when type is unknown
    /// Infers the type based on the identifier pattern
    /// </summary>
    /// <param name="identifier">The raw identifier</param>
    /// <returns>Normalized identifier</returns>
    public static string Normalize(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return string.Empty;

        var normalized = identifier.Trim().ToLowerInvariant();

        // Infer type: if it contains path separators or looks like a domain, don't add .exe
        // Otherwise, if it doesn't end with .exe, add it
        if (normalized.Contains('/') || normalized.Contains('\\'))
        {
            // Path-like identifier, likely exe - add .exe if not present
            if (!normalized.EndsWith(".exe"))
            {
                normalized += ".exe";
            }
        }
        else if (!normalized.Contains('.'))
        {
            // No extension and no dots - likely a process name without extension
            normalized += ".exe";
        }
        // If it contains dots but doesn't end with .exe, it might be:
        // - A domain (github.com) - don't add .exe
        // - An exe with non-standard extension (myapp.app) - add .exe
        // We treat anything that doesn't end with .exe and isn't a path as potentially needing .exe
        // BUT we need to be consistent with the InferType logic

        return normalized;
    }

    /// <summary>
    /// Infers the identifier type from the normalized identifier
    /// </summary>
    /// <param name="normalizedIdentifier">Already normalized identifier</param>
    /// <returns>The inferred AppIdentifierType</returns>
    public static AppIdentifierType InferType(string normalizedIdentifier)
    {
        if (string.IsNullOrWhiteSpace(normalizedIdentifier))
            return AppIdentifierType.Exe;

        if (normalizedIdentifier.EndsWith(".exe"))
            return AppIdentifierType.Exe;

        if (normalizedIdentifier.Contains('/') || normalizedIdentifier.Contains('\\'))
            return AppIdentifierType.Exe;

        return AppIdentifierType.Domain;
    }
}
