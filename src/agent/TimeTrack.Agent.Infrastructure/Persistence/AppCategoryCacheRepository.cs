using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Domain.Entities;
using TimeTrack.Agent.Domain.ValueObjects;

namespace TimeTrack.Agent.Infrastructure.Persistence;

/// <summary>
/// Repository for app category cache in local SQLite
///
/// SRP: Apenas gerencia cache local de categorias
/// DIP: Implementa interface para ser injetado
///
/// CX-143: Sistema de Categorização de Apps/Sites
/// </summary>
public sealed class AppCategoryCacheRepository : IAppCategoryCacheRepository
{
    private readonly SqliteContext _context;
    private readonly ILogger<AppCategoryCacheRepository> _logger;

    public AppCategoryCacheRepository(
        SqliteContext context,
        ILogger<AppCategoryCacheRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AppCategoryCache?> FindByIdentifierAsync(string identifier)
    {
        var normalized = NormalizeIdentifier(identifier);
        await using var gate = await _context.AcquireDbLockAsync();
        var connection = await _context.GetConnectionAsync();

        var row = await connection.QueryFirstOrDefaultAsync<CategoryCacheRow>(
            "SELECT * FROM app_category_cache WHERE identifier = @Identifier",
            new { Identifier = normalized });

        return row != null ? MapToEntity(row) : null;
    }

    public async Task<IReadOnlyList<AppCategoryCache>> GetAllAsync()
    {
        await using var gate = await _context.AcquireDbLockAsync();
        var connection = await _context.GetConnectionAsync();

        var rows = await connection.QueryAsync<CategoryCacheRow>(
            "SELECT * FROM app_category_cache ORDER BY display_name");

        return rows.Select(MapToEntity).ToList();
    }

    public async Task<int> GetVersionAsync()
    {
        await using var gate = await _context.AcquireDbLockAsync();
        var connection = await _context.GetConnectionAsync();

        var version = await connection.QueryFirstOrDefaultAsync<int?>(
            "SELECT version FROM cache_info WHERE cache_type = 'app_categories'");

        return version ?? 0;
    }

    public async Task<DateTime> GetLastSyncAsync()
    {
        await using var gate = await _context.AcquireDbLockAsync();
        var connection = await _context.GetConnectionAsync();

        var lastSync = await connection.QueryFirstOrDefaultAsync<string?>(
            "SELECT last_sync FROM cache_info WHERE cache_type = 'app_categories'");

        return lastSync != null ? DateTime.Parse(lastSync) : DateTime.MinValue;
    }

    public async Task<bool> IsStaleAsync(TimeSpan maxStaleness)
    {
        var lastSync = await GetLastSyncAsync();
        return DateTime.UtcNow - lastSync > maxStaleness;
    }

    public async Task ReplaceAllAsync(IEnumerable<AppCategoryCache> categories, int version)
    {
        await using var gate = await _context.AcquireDbLockAsync();
        var connection = await _context.GetConnectionAsync();

        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // Clear existing cache
            await connection.ExecuteAsync("DELETE FROM app_category_cache");

            // Insert new categories in batches
            var now = DateTime.UtcNow.ToString("O");
            foreach (var category in categories)
            {
                await connection.ExecuteAsync(@"
                    INSERT INTO app_category_cache
                        (id, identifier, identifier_type, display_name, productivity, subcategory, source, version, cached_at, note)
                    VALUES
                        (@Id, @Identifier, @IdentifierType, @DisplayName, @Productivity, @Subcategory, @Source, @Version, @CachedAt, @Note)",
                    new
                    {
                        Id = category.Id.ToString(),
                        category.Identifier,
                        IdentifierType = category.IdentifierType.ToString(),
                        category.DisplayName,
                        Productivity = category.Productivity.ToString(),
                        category.Subcategory,
                        Source = category.Source.ToString(),
                        category.Version,
                        CachedAt = category.CachedAt.ToString("O"),
                        category.Note
                    });
            }

            // Update cache info
            var existingInfo = await connection.QueryFirstOrDefaultAsync<int>(
                "SELECT COUNT(*) FROM cache_info WHERE cache_type = 'app_categories'");

            if (existingInfo == 0)
            {
                await connection.ExecuteAsync(@"
                    INSERT INTO cache_info (cache_type, version, last_sync)
                    VALUES ('app_categories', @Version, @LastSync)",
                    new { Version = version, LastSync = now });
            }
            else
            {
                await connection.ExecuteAsync(@"
                    UPDATE cache_info
                    SET version = @Version, last_sync = @LastSync
                    WHERE cache_type = 'app_categories'",
                    new { Version = version, LastSync = now });
            }

            await transaction.CommitAsync();

            _logger.LogInformation(
                "Replaced {Count} categories in cache (version {Version})",
                categories.Count(),
                version);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to replace category cache");
            throw;
        }
    }

    public async Task ClearAsync()
    {
        await using var gate = await _context.AcquireDbLockAsync();
        var connection = await _context.GetConnectionAsync();

        await connection.ExecuteAsync("DELETE FROM app_category_cache");
        await connection.ExecuteAsync(@"
            UPDATE cache_info
            SET version = 0, last_sync = @MinTime
            WHERE cache_type = 'app_categories'",
            new { MinTime = DateTime.MinValue.ToString("O") });

        _logger.LogInformation("Category cache cleared");
    }

    public async Task<int> CountAsync()
    {
        await using var gate = await _context.AcquireDbLockAsync();
        var connection = await _context.GetConnectionAsync();

        return await connection.QueryFirstOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM app_category_cache");
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

    private static AppCategoryCache MapToEntity(CategoryCacheRow row)
    {
        return new AppCategoryCache
        {
            Id = Guid.Parse(row.id),
            Identifier = row.identifier,
            IdentifierType = Enum.Parse<AppIdentifierType>(row.identifier_type),
            DisplayName = row.display_name,
            Productivity = Enum.Parse<AppProductivityCategory>(row.productivity),
            Subcategory = row.subcategory,
            Source = Enum.Parse<CategorySource>(row.source),
            Version = row.version,
            CachedAt = DateTime.Parse(row.cached_at),
            Note = row.note
        };
    }

    /// <summary>
    /// Row mapping for Dapper
    /// </summary>
    private sealed class CategoryCacheRow
    {
        public string id { get; set; } = string.Empty;
        public string identifier { get; set; } = string.Empty;
        public string identifier_type { get; set; } = string.Empty;
        public string display_name { get; set; } = string.Empty;
        public string productivity { get; set; } = string.Empty;
        public string subcategory { get; set; } = string.Empty;
        public string source { get; set; } = string.Empty;
        public int version { get; set; }
        public string cached_at { get; set; } = string.Empty;
        public string? note { get; set; }
    }
}
