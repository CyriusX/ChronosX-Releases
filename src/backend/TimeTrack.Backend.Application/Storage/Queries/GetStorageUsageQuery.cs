using MediatR;
using TimeTrack.Backend.Application.Storage.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Storage.Queries;

public sealed record GetStorageUsageQuery(Guid OrgId) : IRequest<StorageUsageResponse>;

public sealed class GetStorageUsageQueryHandler
    : IRequestHandler<GetStorageUsageQuery, StorageUsageResponse>
{
    private readonly IOrganizationRepository _orgRepository;

    public GetStorageUsageQueryHandler(IOrganizationRepository orgRepository)
    {
        _orgRepository = orgRepository;
    }

    public async Task<StorageUsageResponse> Handle(
        GetStorageUsageQuery request, CancellationToken ct)
    {
        var org = await _orgRepository.GetByIdAsync(request.OrgId, ct)
            ?? throw new KeyNotFoundException($"Organization {request.OrgId} not found");

        var quotaBytes = (long)(org.StorageQuotaGb * 1024m * 1024m * 1024m);
        var usedBytes = org.StorageUsedBytes;
        var percentage = quotaBytes > 0 ? (double)usedBytes / quotaBytes * 100 : 0;
        var usedGb = (decimal)usedBytes / (1024m * 1024m * 1024m);

        return new StorageUsageResponse
        {
            UsedBytes = usedBytes,
            QuotaBytes = quotaBytes,
            Percentage = Math.Round(percentage, 1),
            QuotaGb = org.StorageQuotaGb,
            UsedGb = Math.Round(usedGb, 2),
        };
    }
}
