using TimeTrack.Agent.Domain.Entities;

namespace TimeTrack.Agent.Contracts.Repositories;

/// <summary>
/// Repository for app category cache in local SQLite
///
/// CX-143: Sistema de Categorização de Apps/Sites
/// </summary>
public interface IAppCategoryCacheRepository
{
    Task<AppCategoryCache?> FindByIdentifierAsync(string identifier);
    Task<IReadOnlyList<AppCategoryCache>> GetAllAsync();
    Task<int> GetVersionAsync();
    Task<DateTime> GetLastSyncAsync();
    Task<bool> IsStaleAsync(TimeSpan maxStaleness);
    Task ReplaceAllAsync(IEnumerable<AppCategoryCache> categories, int version);
    Task ClearAsync();
    Task<int> CountAsync();
}
