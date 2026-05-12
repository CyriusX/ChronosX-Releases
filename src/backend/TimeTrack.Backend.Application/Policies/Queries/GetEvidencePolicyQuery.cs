using MediatR;
using TimeTrack.Backend.Application.Policies.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Policies.Queries;

public sealed record GetEvidencePolicyQuery(Guid OrgId) : IRequest<EvidencePolicyResponse>;

public sealed class GetEvidencePolicyQueryHandler : IRequestHandler<GetEvidencePolicyQuery, EvidencePolicyResponse>
{
    private readonly IOrgPolicyRepository _policyRepository;

    public GetEvidencePolicyQueryHandler(IOrgPolicyRepository policyRepository)
    {
        _policyRepository = policyRepository;
    }

    public async Task<EvidencePolicyResponse> Handle(GetEvidencePolicyQuery request, CancellationToken cancellationToken)
    {
        var policy = await _policyRepository.GetByOrgIdAsync(request.OrgId, cancellationToken);

        if (policy == null)
        {
            return new EvidencePolicyResponse
            {
                ScreenshotsEnabled = false,
                ScreenshotIntervalMinutes = 5,
                ScreenshotExcludedApps = [],
                EvidenceRetentionDays = 30,
                WebsiteTrackingEnabled = true,
                Version = 1
            };
        }

        return new EvidencePolicyResponse
        {
            ScreenshotsEnabled = policy.ScreenshotsEnabled,
            ScreenshotIntervalMinutes = policy.ScreenshotIntervalMinutes,
            ScreenshotExcludedApps = policy.GetScreenshotExcludedApps(),
            EvidenceRetentionDays = policy.EvidenceRetentionDays,
            WebsiteTrackingEnabled = policy.WebsiteTrackingEnabled,
            Version = policy.Version
        };
    }
}
