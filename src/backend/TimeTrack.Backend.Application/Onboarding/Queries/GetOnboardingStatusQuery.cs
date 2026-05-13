using MediatR;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Onboarding.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Onboarding.Queries;

public sealed record GetOnboardingStatusQuery(Guid OrgId) : IRequest<OnboardingStatusResponse>;

public sealed class GetOnboardingStatusQueryHandler : IRequestHandler<GetOnboardingStatusQuery, OnboardingStatusResponse>
{
    private readonly IOrganizationRepository _organizationRepository;

    public GetOnboardingStatusQueryHandler(IOrganizationRepository organizationRepository)
    {
        _organizationRepository = organizationRepository;
    }

    public async Task<OnboardingStatusResponse> Handle(GetOnboardingStatusQuery request, CancellationToken cancellationToken)
    {
        var org = await _organizationRepository.GetByIdAsync(request.OrgId, cancellationToken)
            ?? throw new NotFoundException("Organization", request.OrgId);

        return new OnboardingStatusResponse
        {
            IsComplete = org.OnboardingCompletedAt.HasValue,
            OrgId = request.OrgId
        };
    }
}
