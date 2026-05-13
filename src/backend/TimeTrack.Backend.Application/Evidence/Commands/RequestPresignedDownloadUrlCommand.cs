using MediatR;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Evidence.DTOs;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.Interfaces.Services;

namespace TimeTrack.Backend.Application.Evidence.Commands;

public sealed record RequestPresignedDownloadUrlCommand(
    Guid EvidenceId) : IRequest<PresignedDownloadUrlResponse>;

public sealed class RequestPresignedDownloadUrlCommandHandler
    : IRequestHandler<RequestPresignedDownloadUrlCommand, PresignedDownloadUrlResponse>
{
    private readonly IEvidenceItemRepository _evidenceRepository;
    private readonly IMediaServiceClient _mediaService;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ICurrentUserContext _currentUser;

    public RequestPresignedDownloadUrlCommandHandler(
        IEvidenceItemRepository evidenceRepository,
        IMediaServiceClient mediaService,
        IAuditLogRepository auditLogRepository,
        ICurrentUserContext currentUser)
    {
        _evidenceRepository = evidenceRepository;
        _mediaService = mediaService;
        _auditLogRepository = auditLogRepository;
        _currentUser = currentUser;
    }

    public async Task<PresignedDownloadUrlResponse> Handle(
        RequestPresignedDownloadUrlCommand command, CancellationToken ct)
    {
        var item = await _evidenceRepository.GetByIdAsync(command.EvidenceId, ct)
            ?? throw new KeyNotFoundException($"Evidence item {command.EvidenceId} not found");

        if (item.IsDeleted)
            throw new InvalidOperationException("Cannot access deleted evidence");

        MediaInfo? mediaInfo = null;
        string? presignedDownloadUrl = null;
        if (!string.IsNullOrEmpty(item.ExternalMediaId))
        {
            mediaInfo = await _mediaService.GetMediaAsync(item.ExternalMediaId, ct);

            var presigned = await _mediaService.GetPresignedDownloadUrlAsync(item.ExternalMediaId, ct);
            presignedDownloadUrl = presigned?.DownloadUrl;
        }

        var orgId = _currentUser.OrgId ?? item.OrgId;
        var userId = _currentUser.UserId;

        var auditLog = AuditLog.Create(
            orgId,
            userId,
            "evidence.view",
            "evidence_item",
            item.Id);

        await _auditLogRepository.AddAsync(auditLog, ct);

        return new PresignedDownloadUrlResponse(
            presignedDownloadUrl ?? mediaInfo?.Url ?? string.Empty,
            new EvidenceItemResponse
            {
                Id = item.Id,
                UserId = item.UserId,
                EvidenceType = item.EvidenceType,
                CapturedAt = item.CapturedAt,
                AppName = item.AppName,
                FileSizeBytes = item.FileSizeBytes,
                ThumbnailUrl = mediaInfo?.ThumbnailUrl,
                CreatedAt = item.CreatedAt
            });
    }
}
