using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Domain.Entities;

namespace TimeTrack.Agent.Application.Services;

/// <summary>
/// Service for synchronizing app categories from backend
///
/// SRP: Apenas sincroniza categorias - não resolve categorias
/// OCP: Extensível para diferentes fontes de categorias
/// DIP: Depende de abstrações (repository, http client)
///
/// Composition Pattern: Combina cache local com sincronização remota
///
/// CX-143: Sistema de Categorização de Apps/Sites
/// </summary>
public interface IAppCategorySyncService
{
    /// <summary>
    /// Sync categories from backend if cache is stale
    /// </summary>
    Task<bool> SyncIfNeededAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Force sync categories from backend
    /// </summary>
    Task<bool> ForceSyncAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get current cache version
    /// </summary>
    Task<int> GetCacheVersionAsync();

    /// <summary>
    /// Check if cache is stale
    /// </summary>
    Task<bool> IsCacheStaleAsync();

    /// <summary>
    /// Clear local cache
    /// </summary>
    Task ClearCacheAsync();
}

public sealed class AppCategorySyncService : IAppCategorySyncService
{
    private readonly IAppCategoryCacheRepository _cacheRepo;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AppCategorySyncService> _logger;

    // Maximum staleness before requiring sync (5 minutes)
    private static readonly TimeSpan MaxStaleness = TimeSpan.FromMinutes(5);

    public AppCategorySyncService(
        IAppCategoryCacheRepository cacheRepo,
        HttpClient httpClient,
        ILogger<AppCategorySyncService> logger)
    {
        _cacheRepo = cacheRepo ?? throw new ArgumentNullException(nameof(cacheRepo));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> SyncIfNeededAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var isStale = await _cacheRepo.IsStaleAsync(MaxStaleness);

            if (!isStale)
            {
                _logger.LogDebug("Category cache is fresh, skipping sync");
                return true;
            }

            _logger.LogInformation("Category cache is stale, syncing from backend");
            return await ForceSyncAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check/sync category cache");
            return false;
        }
    }

    public async Task<bool> ForceSyncAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var localVersion = await _cacheRepo.GetVersionAsync();

            // Build API URL - should be injected via configuration
            var apiUrl = "/api/v1/app-categories"; // Relative path, base URL set in HttpClient

            // Fetch from backend with version for conditional request
            var response = await _httpClient.GetAsync(
                $"{apiUrl}?version={localVersion}",
                cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotModified)
            {
                _logger.LogDebug("Backend returned 304, cache is still valid");
                return true;
            }

            response.EnsureSuccessStatusCode();

            var batchResponse = await response.Content.ReadFromJsonAsync<AppCategoryBatchResponse>(
                cancellationToken: cancellationToken);

            if (batchResponse == null || batchResponse.Categories == null)
            {
                _logger.LogWarning("Empty response from category sync endpoint");
                return false;
            }

            // Convert to cache entities
            var cacheEntries = batchResponse.Categories.Select(c =>
                AppCategoryCache.FromResponse(
                    c.Identifier,
                    c.IdentifierType,
                    c.DisplayName,
                    c.Productivity,
                    c.Subcategory,
                    c.Source,
                    batchResponse.Version,
                    c.Note))
                .ToList();

            // Replace local cache
            await _cacheRepo.ReplaceAllAsync(cacheEntries, batchResponse.Version);

            _logger.LogInformation(
                "Synced {Count} categories from backend (version {Version})",
                cacheEntries.Count,
                batchResponse.Version);

            return true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error syncing categories from backend");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync categories from backend");
            return false;
        }
    }

    public async Task<int> GetCacheVersionAsync()
    {
        return await _cacheRepo.GetVersionAsync();
    }

    public async Task<bool> IsCacheStaleAsync()
    {
        return await _cacheRepo.IsStaleAsync(MaxStaleness);
    }

    public async Task ClearCacheAsync()
    {
        await _cacheRepo.ClearAsync();
        _logger.LogInformation("Category cache cleared");
    }
}

/// <summary>
/// Response from the batch category API
/// </summary>
public sealed class AppCategoryBatchResponse
{
    public List<AppCategoryApiResponse>? Categories { get; set; }
    public int Version { get; set; }
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// Single category from API response
/// </summary>
public sealed class AppCategoryApiResponse
{
    public string Identifier { get; set; } = string.Empty;
    public string IdentifierType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Productivity { get; set; } = string.Empty;
    public string Subcategory { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? Note { get; set; }
}
