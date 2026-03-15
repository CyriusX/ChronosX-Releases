using TimeTrack.Agent.Domain.ValueObjects;

namespace TimeTrack.Agent.Domain.Entities;

/// <summary>
/// Cache local de categorias de apps no SQLite do Agent
///
/// SRP: Apenas armazena categorias para lookup offline
/// Sincronizado com backend a cada sync cycle (máx. 5 min de staleness)
///
/// CX-143: Sistema de Categorização de Apps/Sites
/// </summary>
public sealed class AppCategoryCache
{
    public Guid Id { get; set; }
    public string Identifier { get; set; } = string.Empty;
    public AppIdentifierType IdentifierType { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public AppProductivityCategory Productivity { get; set; }
    public string Subcategory { get; set; } = string.Empty;
    public CategorySource Source { get; set; }

    /// <summary>
    /// Version from the last sync - used for cache invalidation
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// When this cache was last updated
    /// </summary>
    public DateTime CachedAt { get; set; }

    /// <summary>
    /// Optional note (from org overrides)
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// Check if cache is stale (older than max staleness)
    /// </summary>
    public bool IsStale(TimeSpan maxStaleness)
    {
        return DateTime.UtcNow - CachedAt > maxStaleness;
    }

    /// <summary>
    /// Create from API response
    /// </summary>
    public static AppCategoryCache FromResponse(
        string identifier,
        string identifierType,
        string displayName,
        string productivity,
        string subcategory,
        string source,
        int version,
        string? note = null)
    {
        return new AppCategoryCache
        {
            Id = Guid.NewGuid(),
            Identifier = identifier.ToLowerInvariant(),
            IdentifierType = ParseIdentifierType(identifierType),
            DisplayName = displayName,
            Productivity = ParseProductivity(productivity),
            Subcategory = subcategory,
            Source = ParseSource(source),
            Version = version,
            CachedAt = DateTime.UtcNow,
            Note = note
        };
    }

    private static AppIdentifierType ParseIdentifierType(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "exe" => AppIdentifierType.Exe,
            "domain" => AppIdentifierType.Domain,
            _ => AppIdentifierType.Exe
        };
    }

    private static AppProductivityCategory ParseProductivity(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "productive" => AppProductivityCategory.Productive,
            "distraction" => AppProductivityCategory.Distraction,
            _ => AppProductivityCategory.Neutral
        };
    }

    private static CategorySource ParseSource(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "global" => CategorySource.Global,
            "org_override" => CategorySource.OrgOverride,
            _ => CategorySource.Default
        };
    }
}
