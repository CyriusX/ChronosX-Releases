using MediatR;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.Evidence.Commands;

public sealed record DeleteEvidenceCommand(Guid EvidenceId) : IRequest<DeleteEvidenceResponse>;

public sealed record DeleteEvidenceResponse(
    Guid Id,
    DateTime DeletedAt,
    string Message);

public sealed class DeleteEvidenceCommandHandler
    : IRequestHandler<DeleteEvidenceCommand, DeleteEvidenceResponse>
{
    private readonly IEvidenceItemRepository _evidenceRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ICurrentUserContext _currentUser;

    public DeleteEvidenceCommandHandler(
        IEvidenceItemRepository evidenceRepository,
        IAuditLogRepository auditLogRepository,
        ICurrentUserContext currentUser)
    {
        _evidenceRepository = evidenceRepository;
        _auditLogRepository = auditLogRepository;
        _currentUser = currentUser;
    }

    public async Task<DeleteEvidenceResponse> Handle(
        DeleteEvidenceCommand command, CancellationToken ct)
    {
        var item = await _evidenceRepository.GetByIdAsync(command.EvidenceId, ct)
            ?? throw new KeyNotFoundException($"Evidence item {command.EvidenceId} not found");

        if (item.IsDeleted)
            throw new KeyNotFoundException($"Evidence item {command.EvidenceId} not found");

        var userId = _currentUser.UserId ?? throw new UnauthorizedAccessException();
        var role = _currentUser.Role ?? UserRole.Colaborador;

        if (role == UserRole.Gestor)
            throw new UnauthorizedAccessException("Managers cannot delete evidence items");

        if (role != UserRole.Admin && item.UserId != userId)
            throw new UnauthorizedAccessException("You can only delete your own evidence items");

        item.SoftDelete();
        await _evidenceRepository.UpdateAsync(item, ct);

        var orgId = _currentUser.OrgId ?? item.OrgId;
        var auditLog = AuditLog.Create(
            orgId,
            userId,
            AuditActions.EvidenceDelete,
            "evidence_item",
            item.Id,
            newValues: $"{{\"target_user_id\":\"{item.UserId}\",\"evidence_id\":\"{item.Id}\"}}");

        await _auditLogRepository.AddAsync(auditLog, ct);

        return new DeleteEvidenceResponse(
            item.Id,
            DateTime.UtcNow,
            "Evidencia marcada para exclusao. O arquivo sera removido em ate 24 horas.");
    }
}
