using System.Text.Json;
using MediatR;
using TimeTrack.Backend.Application.Policies.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Policies.Queries;

/// <summary>
/// Query to get the organization's policy
/// </summary>
public sealed record GetOrgPolicyQuery(Guid OrgId) : IRequest<OrgPolicyResponse>;

/// <summary>
/// Handler for GetOrgPolicyQuery
/// </summary>
public sealed class GetOrgPolicyQueryHandler : IRequestHandler<GetOrgPolicyQuery, OrgPolicyResponse>
{
    private readonly IOrgPolicyRepository _policyRepository;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public GetOrgPolicyQueryHandler(IOrgPolicyRepository policyRepository)
    {
        _policyRepository = policyRepository;
    }

    public async Task<OrgPolicyResponse> Handle(GetOrgPolicyQuery request, CancellationToken cancellationToken)
    {
        var policy = await _policyRepository.GetByOrgIdAsync(request.OrgId, cancellationToken);

        if (policy == null)
        {
            // Return default policy if none exists
            return CreateDefaultPolicyResponse(request.OrgId);
        }

        return MapToResponse(policy);
    }

    private static OrgPolicyResponse CreateDefaultPolicyResponse(Guid orgId)
    {
        return new OrgPolicyResponse
        {
            Id = Guid.Empty,
            OrgId = orgId,
            Version = 1,
            WorkHours = new WorkHoursDto
            {
                Timezone = "America/Sao_Paulo",
                Days = new List<string> { "monday", "tuesday", "wednesday", "thursday", "friday" },
                StartTime = "08:00",
                EndTime = "18:00"
            },
            AppExclusions = new List<string>(),
            IdleThresholdSeconds = 180,
            RetentionDays = 90,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null
        };
    }

    private static OrgPolicyResponse MapToResponse(Domain.Entities.OrgPolicy policy)
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
