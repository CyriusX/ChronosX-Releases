using MediatR;
using TimeTrack.Backend.Application.Audit.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Audit.Queries;

/// <summary>
/// Query para obter audit logs paginados
/// </summary>
public sealed record GetAuditLogsQuery(
    Guid OrgId,
    int Page = 1,
    int Limit = 50,
    string? Action = null,
    Guid? UserId = null) : IRequest<AuditLogListResponse>;

public sealed class GetAuditLogsQueryHandler : IRequestHandler<GetAuditLogsQuery, AuditLogListResponse>
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUserRepository _userRepository;

    public GetAuditLogsQueryHandler(
        IAuditLogRepository auditLogRepository,
        IUserRepository userRepository)
    {
        _auditLogRepository = auditLogRepository;
        _userRepository = userRepository;
    }

    public async Task<AuditLogListResponse> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        // Validate pagination
        var page = Math.Max(1, request.Page);
        var limit = Math.Min(100, Math.Max(1, request.Limit));

        // Get paginated audit logs
        var (items, totalCount) = await _auditLogRepository.GetPagedByOrgAsync(
            request.OrgId,
            page,
            limit,
            request.Action,
            request.UserId,
            cancellationToken);

        // Get user info for audit logs that have a user
        var userIds = items
            .Where(a => a.UserId.HasValue)
            .Select(a => a.UserId!.Value)
            .Distinct()
            .ToList();

        var users = await GetUsersByIdsAsync(userIds, cancellationToken);

        // Map to response
        var auditLogItems = items.Select(a => new AuditLogItem
        {
            Id = a.Id,
            UserId = a.UserId,
            UserEmail = a.UserId.HasValue && users.TryGetValue(a.UserId.Value, out var user) ? user.Email : null,
            UserDisplayName = a.UserId.HasValue && users.TryGetValue(a.UserId.Value, out var u) ? u.DisplayName : null,
            Action = a.Action,
            EntityType = a.EntityType,
            EntityId = a.EntityId,
            Metadata = a.NewValues,
            IpAddress = a.IpAddress,
            UserAgent = a.UserAgent,
            CreatedAt = a.CreatedAt
        }).ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)limit);

        return new AuditLogListResponse
        {
            Items = auditLogItems,
            TotalCount = totalCount,
            Page = page,
            PageSize = limit,
            TotalPages = totalPages
        };
    }

    private async Task<Dictionary<Guid, Domain.Entities.User>> GetUsersByIdsAsync(
        List<Guid> userIds,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, Domain.Entities.User>();

        foreach (var userId in userIds)
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user != null)
            {
                result[userId] = user;
            }
        }

        return result;
    }
}
