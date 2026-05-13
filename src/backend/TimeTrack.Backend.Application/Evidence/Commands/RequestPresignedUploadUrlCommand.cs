using FluentValidation;
using MediatR;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Evidence.DTOs;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.Interfaces.Services;

namespace TimeTrack.Backend.Application.Evidence.Commands;

public sealed record RequestPresignedUploadUrlCommand(
    RequestPresignedUploadUrlRequest Request) : IRequest<PresignedUploadUrlResponse>;

public sealed class RequestPresignedUploadUrlCommandHandler
    : IRequestHandler<RequestPresignedUploadUrlCommand, PresignedUploadUrlResponse>
{
    private readonly IEvidenceItemRepository _evidenceRepository;
    private readonly IMediaServiceClient _mediaService;
    private readonly ICurrentUserContext _currentUser;

    public RequestPresignedUploadUrlCommandHandler(
        IEvidenceItemRepository evidenceRepository,
        IMediaServiceClient mediaService,
        ICurrentUserContext currentUser)
    {
        _evidenceRepository = evidenceRepository;
        _mediaService = mediaService;
        _currentUser = currentUser;
    }

    public async Task<PresignedUploadUrlResponse> Handle(
        RequestPresignedUploadUrlCommand command, CancellationToken ct)
    {
        var orgId = _currentUser.OrgId
            ?? throw new UnauthorizedAccessException("Organization context required");
        var userId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("User context required");

        var uploadResult = await _mediaService.RequestUploadUrlAsync(
            command.Request.FileName,
            command.Request.ContentType,
            Math.Max(command.Request.FileSizeBytes, 1),
            "SCREENSHOT",
            ct);

        var evidenceId = Guid.NewGuid();

        var item = EvidenceItem.Create(
            evidenceId,
            userId,
            orgId,
            command.Request.DeviceId,
            command.Request.EvidenceType,
            uploadResult.Key,
            command.Request.CapturedAt,
            command.Request.AppName,
            command.Request.WindowTitleHash);

        // Store the media service's mediaId for future reference
        item.SetExternalMediaId(uploadResult.MediaId);

        await _evidenceRepository.AddAsync(item, ct);

        return new PresignedUploadUrlResponse(
            uploadResult.UploadUrl,
            evidenceId,
            uploadResult.Key);
    }
}

public sealed class RequestPresignedUploadUrlCommandValidator
    : AbstractValidator<RequestPresignedUploadUrlCommand>
{
    public RequestPresignedUploadUrlCommandValidator()
    {
        RuleFor(x => x.Request.FileName).NotEmpty();
        RuleFor(x => x.Request.EvidenceType).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Request.ContentType).NotEmpty();
        RuleFor(x => x.Request.DeviceId).NotEqual(Guid.Empty);
        RuleFor(x => x.Request.AppName).NotEmpty().MaximumLength(200);
    }
}
