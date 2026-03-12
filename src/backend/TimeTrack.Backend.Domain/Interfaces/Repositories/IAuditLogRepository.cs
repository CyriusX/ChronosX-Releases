using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Domain.Interfaces.Repositories;

/// <summary>
/// Interface para repositório de audit logs
/// </summary>
public interface IAuditLogRepository : IRepository<AuditLog>
{
    /// <summary>
    /// Obtém audit logs paginados por organização
    /// </summary>
    Task<(IEnumerable<AuditLog> Items, int TotalCount)> GetPagedByOrgAsync(
        Guid orgId,
        int page,
        int limit,
        string? action = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém audit logs por usuário
    /// </summary>
    Task<IEnumerable<AuditLog>> GetByUserIdAsync(
        Guid userId,
        int limit = 100,
        CancellationToken cancellationToken = default);
}
