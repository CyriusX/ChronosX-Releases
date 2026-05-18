using MediatR;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Evidence.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.Interfaces.Services;

namespace TimeTrack.Backend.Application.Evidence.Queries;

public sealed record GetEvidenceByPeriodQuery(
    Guid? UserId,
    DateTime StartDate,
    DateTime EndDate,
    string? EvidenceType,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedEvidenceResponse>;

public sealed class PagedEvidenceResponse
{
    public IReadOnlyList<EvidenceItemResponse> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public bool HasMore => TotalCount > Page * PageSize;
}

public sealed class GetEvidenceByPeriodQueryHandler
    : IRequestHandler<GetEvidenceByPeriodQuery, PagedEvidenceResponse>
{
    private readonly IEvidenceItemRepository _evidenceRepository;
    private readonly IMediaServiceClient _mediaService;
    private readonly ICurrentUserContext _currentUser;

    public GetEvidenceByPeriodQueryHandler(
        IEvidenceItemRepository evidenceRepository,
        IMediaServiceClient mediaService,
        ICurrentUserContext currentUser)
    {
        _evidenceRepository = evidenceRepository;
        _mediaService = mediaService;
        _currentUser = currentUser;
    }

    public async Task<PagedEvidenceResponse> Handle(
        GetEvidenceByPeriodQuery query, CancellationToken ct)
    {
        var orgId = _currentUser.OrgId
            ?? throw new UnauthorizedAccessException("Organization context required");

        var offset = (query.Page - 1) * query.PageSize;

        var items = await _evidenceRepository.GetByPeriodAsync(
            orgId,
            query.UserId,
            query.StartDate,
            query.EndDate,
            query.EvidenceType,
            query.PageSize,
            offset,
            ct);

        var totalCount = await _evidenceRepository.CountByOrgAsync(orgId, ct);

        var responses = new List<EvidenceItemResponse>();
        foreach (var item in items)
        {
            string? thumbnailUrl = null;

            if (!string.IsNullOrEmpty(item.ExternalMediaId))
            {
                var media = await _mediaService.GetMediaAsync(item.ExternalMediaId, ct);
                thumbnailUrl = media?.ThumbnailUrl ?? media?.Url;
            }

            responses.Add(new EvidenceItemResponse
            {
                Id = item.Id,
                UserId = item.UserId,
                EvidenceType = item.EvidenceType,
                CapturedAt = item.CapturedAt,
                AppName = item.AppName,
                FileSizeBytes = item.FileSizeBytes,
                ThumbnailUrl = thumbnailUrl,
                CreatedAt = item.CreatedAt
            });
        }

        return new PagedEvidenceResponse
        {
            Items = responses,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }
}
