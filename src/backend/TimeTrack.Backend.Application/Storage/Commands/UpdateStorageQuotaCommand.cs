using MediatR;
using TimeTrack.Backend.Application.Storage.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Storage.Commands;

public sealed record UpdateStorageQuotaCommand(
    Guid OrgId,
    decimal QuotaGb) : IRequest<StorageUsageResponse>;

public sealed class UpdateStorageQuotaCommandHandler
    : IRequestHandler<UpdateStorageQuotaCommand, StorageUsageResponse>
{
    private readonly IOrganizationRepository _orgRepository;

    public UpdateStorageQuotaCommandHandler(IOrganizationRepository orgRepository)
    {
        _orgRepository = orgRepository;
    }

    public async Task<StorageUsageResponse> Handle(
        UpdateStorageQuotaCommand request, CancellationToken ct)
    {
        var org = await _orgRepository.GetByIdAsync(request.OrgId, ct)
            ?? throw new KeyNotFoundException($"Organization {request.OrgId} not found");

        org.SetStorageQuota(request.QuotaGb);
        await _orgRepository.UpdateAsync(org, ct);

        var quotaBytes = (long)(org.StorageQuotaGb * 1024m * 1024m * 1024m);
        var usedBytes = org.StorageUsedBytes;
        var percentage = quotaBytes > 0 ? (double)usedBytes / quotaBytes * 100 : 0;

        return new StorageUsageResponse
        {
            UsedBytes = usedBytes,
            QuotaBytes = quotaBytes,
            Percentage = Math.Round(percentage, 1),
            QuotaGb = org.StorageQuotaGb,
            UsedGb = Math.Round((decimal)usedBytes / (1024m * 1024m * 1024m), 2),
        };
    }
}
