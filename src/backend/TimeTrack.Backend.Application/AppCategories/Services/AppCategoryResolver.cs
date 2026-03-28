using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Application.AppCategories.DTOs;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.AppCategories.Services;

/// <summary>
/// Resolves app categories with priority: Org Override > Global > Default
///
/// SRP: Apenas resolve categorias - não persiste nem classifica
/// OCP: Extensível para novas fontes de categoria
/// DIP: Depende de abstrações (interfaces de repositório)
///
/// Composition Pattern: Combina múltiplos repositórios para resolver
/// </summary>
public sealed class AppCategoryResolver
{
    private readonly IAppCategoryGlobalRepository _globalRepo;
    private readonly IAppCategoryOverrideRepository _overrideRepo;
    private readonly ILogger<AppCategoryResolver> _logger;

    public AppCategoryResolver(
        IAppCategoryGlobalRepository globalRepo,
        IAppCategoryOverrideRepository overrideRepo,
        ILogger<AppCategoryResolver> logger)
    {
        _globalRepo = globalRepo ?? throw new ArgumentNullException(nameof(globalRepo));
        _overrideRepo = overrideRepo ?? throw new ArgumentNullException(nameof(overrideRepo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Resolve a categoria de um app com prioridade: Override > Global > Default
    /// </summary>
    public async Task<AppCategoryResponse> ResolveAsync(
        string identifier,
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return CreateDefaultResponse(identifier ?? "unknown");

        var normalizedIdentifier = IdentifierNormalizer.Normalize(identifier);
        var identifierType = IdentifierNormalizer.InferType(normalizedIdentifier);

        // 1. Check for org override (highest priority)
        var categoryOverride = await _overrideRepo.FindAsync(orgId, normalizedIdentifier, cancellationToken);
        if (categoryOverride != null)
        {
            _logger.LogDebug(
                "Category resolved from override: {Identifier} -> {Productivity}",
                normalizedIdentifier,
                categoryOverride.Productivity);

            return MapOverrideToResponse(categoryOverride);
        }

        // 2. Check global list
        var global = await _globalRepo.FindByIdentifierAsync(normalizedIdentifier, cancellationToken);
        if (global != null)
        {
            _logger.LogDebug(
                "Category resolved from global: {Identifier} -> {Productivity}",
                normalizedIdentifier,
                global.Productivity);

            return MapGlobalToResponse(global);
        }

        // 3. Default: neutral/unknown
        _logger.LogDebug(
            "Category not found, using default: {Identifier} -> neutral/unknown",
            normalizedIdentifier);

        return CreateDefaultResponse(normalizedIdentifier, identifierType);
    }

    /// <summary>
    /// Resolve multiple categories in batch (efficient for Agent sync)
    /// </summary>
    public async Task<IReadOnlyList<AppCategoryResponse>> ResolveBatchAsync(
        IEnumerable<string> identifiers,
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        var identifierList = identifiers
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Select(id => IdentifierNormalizer.Normalize(id))
            .Distinct()
            .ToList();

        if (identifierList.Count == 0)
            return Array.Empty<AppCategoryResponse>();

        // Fetch all overrides for this org in one query
        var overrides = await _overrideRepo.GetByOrgAndIdentifiersAsync(
            orgId,
            identifierList,
            cancellationToken);

        var overrideDict = overrides.ToDictionary(o => o.Identifier, o => o);

        // Fetch all globals in one query
        var globals = await _globalRepo.GetByIdentifiersAsync(
            identifierList,
            cancellationToken);

        var globalDict = globals.ToDictionary(g => g.Identifier, g => g);

        // Resolve each identifier
        var results = new List<AppCategoryResponse>();
        foreach (var identifier in identifierList)
        {
            if (overrideDict.TryGetValue(identifier, out var categoryOverride))
            {
                results.Add(MapOverrideToResponse(categoryOverride));
            }
            else if (globalDict.TryGetValue(identifier, out var global))
            {
                results.Add(MapGlobalToResponse(global));
            }
            else
            {
                results.Add(CreateDefaultResponse(identifier));
            }
        }

        return results;
    }

    /// <summary>
    /// Get all categories for an org (merged global + overrides)
    /// Used by Agent for initial sync
    /// </summary>
    public async Task<AppCategoryBatchResponse> GetAllForOrgAsync(
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        // Get all globals
        var globals = await _globalRepo.GetAllAsync(cancellationToken);

        // Get all overrides for this org
        var overrides = await _overrideRepo.GetByOrgIdAsync(orgId, cancellationToken);
        var overrideDict = overrides.ToDictionary(o => o.Identifier, o => o);

        // Merge: start with globals, apply overrides
        var results = new List<AppCategoryResponse>();

        foreach (var global in globals)
        {
            if (overrideDict.TryGetValue(global.Identifier, out var categoryOverride))
            {
                results.Add(MapOverrideToResponse(categoryOverride));
                overrideDict.Remove(global.Identifier); // Mark as used
            }
            else
            {
                results.Add(MapGlobalToResponse(global));
            }
        }

        // Add remaining overrides (for apps not in global list)
        foreach (var remainingOverride in overrideDict.Values)
        {
            results.Add(MapOverrideToResponse(remainingOverride));
        }

        return new AppCategoryBatchResponse
        {
            Categories = results,
            Version = GenerateVersion(),
            GeneratedAt = DateTime.UtcNow
        };
    }

    // ========================================================================
    // Private Helper Methods
    // ========================================================================

    private static AppCategoryResponse MapOverrideToResponse(AppCategoryOverride categoryOverride)
    {
        return new AppCategoryResponse
        {
            Identifier = categoryOverride.Identifier,
            IdentifierType = categoryOverride.IdentifierType.ToString().ToLowerInvariant(),
            DisplayName = categoryOverride.DisplayName ?? FormatDisplayName(categoryOverride.Identifier),
            Productivity = categoryOverride.Productivity.ToString().ToLowerInvariant(),
            Subcategory = MapSubcategoryToString(categoryOverride.Subcategory),
            Source = "org_override",
            Note = categoryOverride.Note
        };
    }

    private static AppCategoryResponse MapGlobalToResponse(AppCategoryGlobal global)
    {
        return new AppCategoryResponse
        {
            Identifier = global.Identifier,
            IdentifierType = global.IdentifierType.ToString().ToLowerInvariant(),
            DisplayName = global.DisplayName,
            Productivity = global.Productivity.ToString().ToLowerInvariant(),
            Subcategory = MapSubcategoryToString(global.Subcategory),
            Source = "global"
        };
    }

    private static AppCategoryResponse CreateDefaultResponse(
        string identifier,
        AppIdentifierType? identifierType = null)
    {
        var inferredType = identifierType ?? IdentifierNormalizer.InferType(identifier);
        return new AppCategoryResponse
        {
            Identifier = identifier,
            IdentifierType = inferredType.ToString().ToLowerInvariant(),
            DisplayName = FormatDisplayName(identifier),
            Productivity = "neutral",
            Subcategory = "unknown",
            Source = "default"
        };
    }

    private static string FormatDisplayName(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return "Unknown";

        // Remove .exe extension for display
        var name = identifier.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);

        // Capitalize first letter
        if (name.Length > 0)
        {
            name = char.ToUpperInvariant(name[0]) + name.Substring(1);
        }

        // Replace dots and hyphens with spaces
        name = name.Replace('.', ' ').Replace('-', ' ');

        return name;
    }

    private static string MapSubcategoryToString(AppSubcategory subcategory)
    {
        // Convert PascalCase to snake_case
        var name = subcategory.ToString();
        return string.Concat(
            name.Select((c, i) =>
                i > 0 && char.IsUpper(c) ? "_" + char.ToLower(c) : char.ToLower(c).ToString())
        );
    }

    private static int GenerateVersion()
    {
        // Simple version based on current date/time
        // In production, use a proper versioning scheme
        return (int)(DateTime.UtcNow.Ticks % int.MaxValue);
    }
}
