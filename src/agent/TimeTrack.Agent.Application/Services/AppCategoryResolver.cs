using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Domain.ValueObjects;

namespace TimeTrack.Agent.Application.Services;

/// <summary>
/// Resolves app categories using local cache (offline-first)
///
/// SRP: Apenas resolve categorias - não sincroniza
/// DIP: Depende de abstrações (repository)
///
/// CX-143: Sistema de Categorização de Apps/Sites
/// </summary>
public interface IAppCategoryResolver
{
    /// <summary>
    /// Resolve category for a single app
    /// </summary>
    Task<AppCategoryResult> ResolveAsync(
        string identifier,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve categories for multiple apps in batch
    /// </summary>
    Task<IReadOnlyList<AppCategoryResult>> ResolveBatchAsync(
        IEnumerable<string> identifiers,
        CancellationToken cancellationToken = default);
}

public sealed class AppCategoryResolver : IAppCategoryResolver
{
    private readonly IAppCategoryCacheRepository _cacheRepo;
    private readonly ILogger<AppCategoryResolver> _logger;

    public AppCategoryResolver(
        IAppCategoryCacheRepository cacheRepo,
        ILogger<AppCategoryResolver> logger)
    {
        _cacheRepo = cacheRepo ?? throw new ArgumentNullException(nameof(cacheRepo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AppCategoryResult> ResolveAsync(
        string identifier,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return AppCategoryResult.Default(identifier ?? "unknown");

        var normalizedIdentifier = NormalizeIdentifier(identifier);

        var cached = await _cacheRepo.FindByIdentifierAsync(normalizedIdentifier);

        if (cached != null)
        {
            return new AppCategoryResult
            {
                Identifier = cached.Identifier,
                IdentifierType = cached.IdentifierType.ToString().ToLowerInvariant(),
                DisplayName = cached.DisplayName,
                Productivity = cached.Productivity,
                Subcategory = cached.Subcategory,
                Source = cached.Source.ToString().ToLowerInvariant(),
                Note = cached.Note
            };
        }

        // Not found in cache - return default
        _logger.LogDebug("App not found in cache: {Identifier}", normalizedIdentifier);

        return AppCategoryResult.Default(normalizedIdentifier);
    }

    public async Task<IReadOnlyList<AppCategoryResult>> ResolveBatchAsync(
        IEnumerable<string> identifiers,
        CancellationToken cancellationToken = default)
    {
        var identifierList = identifiers
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Select(NormalizeIdentifier)
            .Distinct()
            .ToList();

        if (identifierList.Count == 0)
            return Array.Empty<AppCategoryResult>();

        // Get all cached categories
        var allCached = await _cacheRepo.GetAllAsync();
        var cacheDict = allCached.ToDictionary(c => c.Identifier, c => c);

        var results = new List<AppCategoryResult>();

        foreach (var identifier in identifierList)
        {
            if (cacheDict.TryGetValue(identifier, out var cached))
            {
                results.Add(new AppCategoryResult
                {
                    Identifier = cached.Identifier,
                    IdentifierType = cached.IdentifierType.ToString().ToLowerInvariant(),
                    DisplayName = cached.DisplayName,
                    Productivity = cached.Productivity,
                    Subcategory = cached.Subcategory,
                    Source = cached.Source.ToString().ToLowerInvariant(),
                    Note = cached.Note
                });
            }
            else
            {
                results.Add(AppCategoryResult.Default(identifier));
            }
        }

        return results;
    }

    private static string NormalizeIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return "unknown";

        var normalized = identifier.Trim().ToLowerInvariant();

        // Add .exe if it looks like an exe name without extension
        if (!normalized.Contains('.') && !normalized.Contains('/') && !normalized.Contains('\\'))
        {
            normalized += ".exe";
        }

        return normalized;
    }
}

/// <summary>
/// Result of category resolution
/// </summary>
public sealed class AppCategoryResult
{
    public string Identifier { get; set; } = string.Empty;
    public string IdentifierType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public AppProductivityCategory Productivity { get; set; } = AppProductivityCategory.Neutral;
    public string Subcategory { get; set; } = "unknown";
    public string Source { get; set; } = "default";
    public string? Note { get; set; }

    /// <summary>
    /// Create a default result for unknown apps
    /// </summary>
    public static AppCategoryResult Default(string identifier)
    {
        var normalized = identifier?.Trim().ToLowerInvariant() ?? "unknown";

        return new AppCategoryResult
        {
            Identifier = normalized,
            IdentifierType = DetermineIdentifierType(normalized),
            DisplayName = FormatDisplayName(normalized),
            Productivity = AppProductivityCategory.Neutral,
            Subcategory = "unknown",
            Source = "default"
        };
    }

    private static string DetermineIdentifierType(string identifier)
    {
        if (identifier.EndsWith(".exe"))
            return "exe";
        if (identifier.Contains('/') || identifier.Contains('\\'))
            return "exe";
        return "domain";
    }

    private static string FormatDisplayName(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return "Unknown";

        var name = identifier.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);

        if (name.Length > 0)
        {
            name = char.ToUpperInvariant(name[0]) + name.Substring(1);
        }

        name = name.Replace('.', ' ').Replace('-', ' ');
        return name;
    }
}
