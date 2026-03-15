using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.AppCategories.DTOs;

/// <summary>
/// Response DTO for app category resolution
///
/// SRP: Apenas transporta dados de categoria resolvida
/// </summary>
public record AppCategoryResponse
{
    /// <summary>
    /// Identifier (process name or domain)
    /// </summary>
    public required string Identifier { get; init; }

    /// <summary>
    /// Type: exe | domain
    /// </summary>
    public required string IdentifierType { get; init; }

    /// <summary>
    /// Human-readable display name
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Productivity classification: productive | neutral | distraction
    /// </summary>
    public required string Productivity { get; init; }

    /// <summary>
    /// Granular category (development, communication, social_media, etc.)
    /// </summary>
    public required string Subcategory { get; init; }

    /// <summary>
    /// Source of classification: global | org_override | default
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Admin note (only for overrides)
    /// </summary>
    public string? Note { get; init; }
}

/// <summary>
/// Response DTO for override with full details
/// </summary>
public sealed record AppCategoryOverrideResponse : AppCategoryResponse
{
    public Guid Id { get; init; }
    public Guid OrgId { get; init; }
    public Guid CreatedBy { get; init; }
    public string CreatedByName { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Request DTO for creating/updating an override
/// </summary>
public sealed record UpsertAppCategoryOverrideRequest
{
    public required string Identifier { get; init; }
    public required string IdentifierType { get; init; }  // "exe" | "domain"
    public string? DisplayName { get; init; }
    public required string Productivity { get; init; }    // "productive" | "neutral" | "distraction"
    public required string Subcategory { get; init; }
    public string? Note { get; init; }  // Admin justification
}

/// <summary>
/// Response for batch category lookup (used by Agent)
/// </summary>
public sealed record AppCategoryBatchResponse
{
    public required IReadOnlyList<AppCategoryResponse> Categories { get; init; }
    public required int Version { get; init; }  // For cache invalidation
    public required DateTime GeneratedAt { get; init; }
}

/// <summary>
/// Response for listing overrides with pagination
/// </summary>
public sealed record AppCategoryOverrideListResponse
{
    public required IReadOnlyList<AppCategoryOverrideResponse> Overrides { get; init; }
    public required int TotalCount { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
}

/// <summary>
/// Response for global category search
/// </summary>
public sealed record GlobalCategorySearchResponse
{
    public required IReadOnlyList<GlobalCategoryItem> Items { get; init; }
    public required int TotalCount { get; init; }
}

public sealed record GlobalCategoryItem
{
    public required string Identifier { get; init; }
    public required string IdentifierType { get; init; }
    public required string DisplayName { get; init; }
    public required string Productivity { get; init; }
    public required string Subcategory { get; init; }

    /// <summary>
    /// Whether this org already has an override for this identifier
    /// </summary>
    public bool HasOverride { get; init; }
}

/// <summary>
/// Statistics for category usage (for Admin dashboard)
/// </summary>
public sealed record CategoryUsageStatsResponse
{
    public required IReadOnlyList<CategoryUsageItem> TopProductiveApps { get; init; }
    public required IReadOnlyList<CategoryUsageItem> TopNeutralApps { get; init; }
    public required IReadOnlyList<CategoryUsageItem> TopDistractionApps { get; init; }
    public required IReadOnlyList<UncategorizedAppItem> UncategorizedApps { get; init; }
    public required int TotalAppsUsed { get; init; }
    public required int CategorizedApps { get; init; }
    public required int UncategorizedCount { get; init; }
}

public sealed record CategoryUsageItem
{
    public required string Identifier { get; init; }
    public required string DisplayName { get; init; }
    public required string Productivity { get; init; }
    public required string Subcategory { get; init; }
    public required long TotalMinutes { get; init; }
    public required int SessionCount { get; init; }
    public required string Source { get; init; }
}

public sealed record UncategorizedAppItem
{
    public required string Identifier { get; init; }
    public required string IdentifierType { get; init; }
    public required long TotalMinutes { get; init; }
    public required int SessionCount { get; init; }
    public required int UserCount { get; init; }
}
