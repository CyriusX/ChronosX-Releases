using MediatR;
using TimeTrack.Backend.Application.Audit.DTOs;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Audit.Queries;

public sealed record GetEvidenceAccessLogsQuery(
    Guid OrgId,
    int Page = 1,
    int PageSize = 20,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    Guid? ActorUserId = null,
    Guid? TargetUserId = null,
    string? Action = null) : IRequest<EvidenceAccessLogResponse>;

public sealed class GetEvidenceAccessLogsQueryHandler
    : IRequestHandler<GetEvidenceAccessLogsQuery, EvidenceAccessLogResponse>
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IUserRepository _userRepository;

    public GetEvidenceAccessLogsQueryHandler(
        IAuditLogRepository auditLogRepository,
        IUserRepository userRepository)
    {
        _auditLogRepository = auditLogRepository;
        _userRepository = userRepository;
    }

    public async Task<EvidenceAccessLogResponse> Handle(
        GetEvidenceAccessLogsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Min(100, Math.Max(1, request.PageSize));

        var evidenceActions = new[] { "evidence.view", "evidence.download", "evidence.delete" };
        var actionFilter = !string.IsNullOrEmpty(request.Action) ? new[] { request.Action } : evidenceActions;

        string? targetSubstring = request.TargetUserId.HasValue ? request.TargetUserId.Value.ToString() : null;

        var (items, totalCount) = await _auditLogRepository.GetEvidenceAccessLogsAsync(
            request.OrgId,
            page,
            pageSize,
            actionFilter,
            request.StartDate,
            request.EndDate,
            request.ActorUserId,
            targetSubstring,
            ct);

        var actorIds = items.Where(a => a.UserId.HasValue).Select(a => a.UserId!.Value).Distinct().ToList();
        var users = new Dictionary<Guid, User>();
        foreach (var uid in actorIds)
        {
            var user = await _userRepository.GetByIdAsync(uid, ct);
            if (user != null) users[uid] = user;
        }

        var logItems = items.Select(a => new EvidenceAccessLogItem
        {
            Id = a.Id,
            ActorUserId = a.UserId,
            ActorName = a.UserId.HasValue && users.TryGetValue(a.UserId.Value, out var u) ? u.DisplayName : null,
            ActorEmail = a.UserId.HasValue && users.TryGetValue(a.UserId.Value, out var ue) ? ue.Email : null,
            TargetUserId = ParseTargetUserId(a.NewValues),
            EvidenceId = a.EntityId,
            Action = a.Action,
            AccessedAt = a.CreatedAt,
            IpAddress = a.IpAddress,
        }).ToList();

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new EvidenceAccessLogResponse
        {
            Items = logItems,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages,
        };
    }

    private static Guid? ParseTargetUserId(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("target_user_id", out var prop)
                && prop.ValueKind == System.Text.Json.JsonValueKind.String
                && Guid.TryParse(prop.GetString(), out var guid))
                return guid;
        }
        catch { }
        return null;
    }
}
