using MediatR;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Evidence.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.Interfaces.Services;

namespace TimeTrack.Backend.Application.Evidence.Commands;

public sealed record ConfirmUploadCommand(
    Guid EvidenceId,
    long FileSizeBytes) : IRequest<EvidenceItemResponse>;

public sealed class ConfirmUploadCommandHandler
    : IRequestHandler<ConfirmUploadCommand, EvidenceItemResponse>
{
    private readonly IEvidenceItemRepository _evidenceRepository;
    private readonly IMediaServiceClient _mediaService;
    private readonly IOrganizationRepository _orgRepository;
    private readonly ICurrentUserContext _currentUser;

    public ConfirmUploadCommandHandler(
        IEvidenceItemRepository evidenceRepository,
        IMediaServiceClient mediaService,
        IOrganizationRepository orgRepository,
        ICurrentUserContext currentUser)
    {
        _evidenceRepository = evidenceRepository;
        _mediaService = mediaService;
        _orgRepository = orgRepository;
        _currentUser = currentUser;
    }

    public async Task<EvidenceItemResponse> Handle(ConfirmUploadCommand command, CancellationToken ct)
    {
        var item = await _evidenceRepository.GetByIdAsync(command.EvidenceId, ct)
            ?? throw new KeyNotFoundException($"Evidence item {command.EvidenceId} not found");

        // Check storage quota before confirming
        var orgId = _currentUser.OrgId ?? item.OrgId;
        var org = await _orgRepository.GetByIdAsync(orgId, ct);
        if (org != null && org.IsStorageQuotaExceeded())
            throw new InvalidOperationException("Storage quota exceeded. Contact your admin to increase the limit.");

        // Confirm upload in the media service
        if (!string.IsNullOrEmpty(item.ExternalMediaId))
        {
            await _mediaService.ConfirmUploadAsync(item.ExternalMediaId, item.StorageKey, ct);
        }

        item.SetFileSize(command.FileSizeBytes);
        await _evidenceRepository.UpdateAsync(item, ct);

        // Increment org storage usage
        if (org != null)
        {
            org.IncrementStorageUsage(command.FileSizeBytes);
            await _orgRepository.UpdateAsync(org, ct);
        }

        return new EvidenceItemResponse
        {
            Id = item.Id,
            UserId = item.UserId,
            EvidenceType = item.EvidenceType,
            CapturedAt = item.CapturedAt,
            AppName = item.AppName,
            FileSizeBytes = item.FileSizeBytes,
            CreatedAt = item.CreatedAt
        };
    }
}
