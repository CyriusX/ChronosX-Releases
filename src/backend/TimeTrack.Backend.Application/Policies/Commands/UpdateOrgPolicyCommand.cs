using System.Text.Json;
using FluentValidation;
using MediatR;
using TimeTrack.Backend.Application.Policies.DTOs;
using TimeTrack.Backend.Application.Policies.Validators;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Policies.Commands;

/// <summary>
/// Command to update the organization's policy
/// </summary>
public sealed record UpdateOrgPolicyCommand(
    Guid OrgId,
    UpdateOrgPolicyRequest Request) : IRequest<OrgPolicyResponse>;

/// <summary>
/// Handler for UpdateOrgPolicyCommand
/// </summary>
public sealed class UpdateOrgPolicyCommandHandler : IRequestHandler<UpdateOrgPolicyCommand, OrgPolicyResponse>
{
    private readonly IOrgPolicyRepository _policyRepository;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public UpdateOrgPolicyCommandHandler(IOrgPolicyRepository policyRepository)
    {
        _policyRepository = policyRepository;
    }

    public async Task<OrgPolicyResponse> Handle(UpdateOrgPolicyCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;

        // Get existing policy or create new one
        var policy = await _policyRepository.GetByOrgIdAsync(command.OrgId, cancellationToken);

        if (policy == null)
        {
            policy = OrgPolicy.Create(command.OrgId);
            await _policyRepository.AddAsync(policy, cancellationToken);
        }

        // Prepare updated values
        string? workHoursJson = null;
        string? appExclusionsJson = null;

        if (request.WorkHours != null)
        {
            var workHours = new WorkHoursConfig
            {
                Timezone = request.WorkHours.Timezone,
                Days = request.WorkHours.Days,
                StartTime = request.WorkHours.StartTime,
                EndTime = request.WorkHours.EndTime
            };
            workHoursJson = JsonSerializer.Serialize(workHours, JsonOptions);
        }

        if (request.AppExclusions != null)
        {
            appExclusionsJson = JsonSerializer.Serialize(request.AppExclusions, JsonOptions);
        }

        // Update policy
        policy.Update(
            workHoursJson,
            appExclusionsJson,
            request.IdleThresholdSeconds,
            request.RetentionDays);

        await _policyRepository.UpdateAsync(policy, cancellationToken);

        // Return updated policy
        return MapToResponse(policy);
    }

    private static OrgPolicyResponse MapToResponse(OrgPolicy policy)
    {
        var workHours = policy.GetWorkHours();
        var appExclusions = policy.GetAppExclusions();

        return new OrgPolicyResponse
        {
            Id = policy.Id,
            OrgId = policy.OrgId,
            Version = policy.Version,
            WorkHours = workHours != null
                ? new WorkHoursDto
                {
                    Timezone = workHours.Timezone,
                    Days = workHours.Days,
                    StartTime = workHours.StartTime,
                    EndTime = workHours.EndTime
                }
                : new WorkHoursDto(),
            AppExclusions = appExclusions ?? new List<string>(),
            IdleThresholdSeconds = policy.IdleThresholdSeconds,
            RetentionDays = policy.RetentionDays,
            CreatedAt = policy.CreatedAt,
            UpdatedAt = policy.UpdatedAt
        };
    }
}

/// <summary>
/// Validator for UpdateOrgPolicyCommand
/// </summary>
public sealed class UpdateOrgPolicyCommandValidator : AbstractValidator<UpdateOrgPolicyCommand>
{
    public UpdateOrgPolicyCommandValidator()
    {
        RuleFor(x => x.Request)
            .SetValidator(new UpdateOrgPolicyRequestValidator());
    }
}
