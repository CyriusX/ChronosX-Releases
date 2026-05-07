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
            IdleJustificationPromptThresholdSeconds = null,
            RetentionDays = 90,
            FocusMode = new FocusModeDto
            {
                Enabled = false,
                Mode = "none",
                AllowUserOverride = true,
                Pomodoro = new PomodoroConfigDto
                {
                    FocusMinutes = 25,
                    ShortBreakMinutes = 5,
                    LongBreakMinutes = 15,
                    CyclesBeforeLongBreak = 4
                },
                Ultradian = new UltradianConfigDto
                {
                    FocusMinutes = 90,
                    BreakMinutes = 20
                }
            },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null
        };
    }

    private static OrgPolicyResponse MapToResponse(Domain.Entities.OrgPolicy policy)
    {
        var workHours = policy.GetWorkHours();
        var appExclusions = policy.GetAppExclusions();
        var focusMode = policy.GetFocusMode();

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
            IdleJustificationPromptThresholdSeconds = policy.IdleJustificationPromptThresholdSeconds,
            RetentionDays = policy.RetentionDays,
            FocusMode = focusMode != null
                ? new FocusModeDto
                {
                    Enabled = focusMode.Enabled,
                    Mode = focusMode.Mode,
                    AllowUserOverride = focusMode.AllowUserOverride,
                    Pomodoro = focusMode.Pomodoro != null
                        ? new PomodoroConfigDto
                        {
                            FocusMinutes = focusMode.Pomodoro.FocusMinutes,
                            ShortBreakMinutes = focusMode.Pomodoro.ShortBreakMinutes,
                            LongBreakMinutes = focusMode.Pomodoro.LongBreakMinutes,
                            CyclesBeforeLongBreak = focusMode.Pomodoro.CyclesBeforeLongBreak
                        }
                        : null,
                    Ultradian = focusMode.Ultradian != null
                        ? new UltradianConfigDto
                        {
                            FocusMinutes = focusMode.Ultradian.FocusMinutes,
                            BreakMinutes = focusMode.Ultradian.BreakMinutes
                        }
                        : null
                }
                : new FocusModeDto(),
            CreatedAt = policy.CreatedAt,
            UpdatedAt = policy.UpdatedAt
        };
    }
}
