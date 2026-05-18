using FluentValidation;
using MediatR;
using TimeTrack.Backend.Application.Policies.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Policies.Commands;

public sealed record UpdateEvidencePolicyCommand(
    Guid OrgId,
    UpdateEvidencePolicyRequest Request) : IRequest<EvidencePolicyResponse>;

public sealed class UpdateEvidencePolicyCommandHandler : IRequestHandler<UpdateEvidencePolicyCommand, EvidencePolicyResponse>
{
    private readonly IOrgPolicyRepository _policyRepository;

    public UpdateEvidencePolicyCommandHandler(IOrgPolicyRepository policyRepository)
    {
        _policyRepository = policyRepository;
    }

    public async Task<EvidencePolicyResponse> Handle(UpdateEvidencePolicyCommand command, CancellationToken cancellationToken)
    {
        var policy = await _policyRepository.GetByOrgIdAsync(command.OrgId, cancellationToken);
        var isNewPolicy = policy == null;

        if (policy == null)
            policy = Domain.Entities.OrgPolicy.Create(command.OrgId);

        policy.UpdateEvidencePolicy(
            command.Request.ScreenshotsEnabled,
            command.Request.ScreenshotIntervalMinutes,
            command.Request.ScreenshotExcludedApps,
            command.Request.EvidenceRetentionDays,
            command.Request.WebsiteTrackingEnabled);

        if (isNewPolicy)
            await _policyRepository.AddAsync(policy, cancellationToken);
        else
            await _policyRepository.UpdateAsync(policy, cancellationToken);

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

public sealed class UpdateEvidencePolicyCommandValidator : AbstractValidator<UpdateEvidencePolicyCommand>
{
    public UpdateEvidencePolicyCommandValidator()
    {
        RuleFor(x => x.Request.ScreenshotIntervalMinutes)
            .InclusiveBetween(1, 60)
            .When(x => x.Request.ScreenshotIntervalMinutes.HasValue);

        RuleFor(x => x.Request.EvidenceRetentionDays)
            .InclusiveBetween(7, 90)
            .When(x => x.Request.EvidenceRetentionDays.HasValue);

        RuleFor(x => x.Request.ScreenshotExcludedApps)
            .Must(list => list == null || list.Count <= 50)
            .WithMessage("Maximum 50 excluded apps allowed");
    }
}
