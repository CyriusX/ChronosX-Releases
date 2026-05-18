using MediatR;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.Interfaces.Services;

namespace TimeTrack.Backend.Application.Evidence.Queries;

public sealed record BatchDownloadUrlItem(
    Guid EvidenceId,
    string? DownloadUrl);

public sealed record GetBatchDownloadUrlsQuery(
    IReadOnlyList<Guid> EvidenceIds) : IRequest<IReadOnlyList<BatchDownloadUrlItem>>;

public sealed class GetBatchDownloadUrlsQueryHandler
    : IRequestHandler<GetBatchDownloadUrlsQuery, IReadOnlyList<BatchDownloadUrlItem>>
{
    private readonly IEvidenceItemRepository _evidenceRepository;
    private readonly IMediaServiceClient _mediaService;
    private readonly ILogger<GetBatchDownloadUrlsQueryHandler> _logger;

    public GetBatchDownloadUrlsQueryHandler(
        IEvidenceItemRepository evidenceRepository,
        IMediaServiceClient mediaService,
        ILogger<GetBatchDownloadUrlsQueryHandler> logger)
    {
        _evidenceRepository = evidenceRepository;
        _mediaService = mediaService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<BatchDownloadUrlItem>> Handle(
        GetBatchDownloadUrlsQuery query, CancellationToken ct)
    {
        var tasks = query.EvidenceIds.Select(async id =>
        {
            try
            {
                var item = await _evidenceRepository.GetByIdAsync(id, ct);
                if (item is null || item.IsDeleted || string.IsNullOrEmpty(item.ExternalMediaId))
                    return new BatchDownloadUrlItem(id, null);

                var presigned = await _mediaService.GetPresignedDownloadUrlAsync(item.ExternalMediaId, ct);
                return new BatchDownloadUrlItem(id, presigned?.DownloadUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get download URL for evidence {EvidenceId}", id);
                return new BatchDownloadUrlItem(id, null);
            }
        });

        return await Task.WhenAll(tasks);
    }
}
