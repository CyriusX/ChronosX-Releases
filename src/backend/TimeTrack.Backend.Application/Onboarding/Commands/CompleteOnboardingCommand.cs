using MediatR;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Onboarding.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Onboarding.Commands;

public sealed record CompleteOnboardingCommand(Guid OrgId) : IRequest<CompleteOnboardingResponse>;

public sealed class CompleteOnboardingCommandHandler : IRequestHandler<CompleteOnboardingCommand, CompleteOnboardingResponse>
{
    private readonly IOrganizationRepository _organizationRepository;

    public CompleteOnboardingCommandHandler(IOrganizationRepository organizationRepository)
    {
        _organizationRepository = organizationRepository;
    }

    public async Task<CompleteOnboardingResponse> Handle(CompleteOnboardingCommand request, CancellationToken cancellationToken)
    {
        var org = await _organizationRepository.GetByIdAsync(request.OrgId, cancellationToken)
            ?? throw new NotFoundException("Organization", request.OrgId);

        org.CompleteOnboarding();
        await _organizationRepository.UpdateAsync(org, cancellationToken);

        return new CompleteOnboardingResponse { Success = true };
    }
}
