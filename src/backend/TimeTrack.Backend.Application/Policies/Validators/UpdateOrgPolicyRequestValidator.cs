using FluentValidation;
using System.Collections.Generic;
using System.Linq;
using TimeTrack.Backend.Application.Policies.DTOs;

namespace TimeTrack.Backend.Application.Policies.Validators;

/// <summary>
/// Validator for UpdateOrgPolicyRequest
/// SRP: Apenas orquestra validação da requisição de atualização de política
/// Delega validações específicas para validators dedicados
/// </summary>
public sealed class UpdateOrgPolicyRequestValidator : AbstractValidator<UpdateOrgPolicyRequest>
{
    private static readonly HashSet<int> ValidRetentionDays = new() { 30, 60, 90, 180, 365 };

    public UpdateOrgPolicyRequestValidator()
    {
        RuleFor(x => x.WorkHours)
            .SetValidator(new WorkHoursDtoValidator()!)
            .When(x => x.WorkHours is not null);

        RuleFor(x => x.AppExclusions)
            .Must(BeValidAppExclusions)
            .WithMessage("App exclusions must be a list of non-empty strings")
            .When(x => x.AppExclusions is not null);

        RuleFor(x => x.IdleThresholdSeconds)
            .InclusiveBetween(60, 3600)
            .WithMessage("Idle threshold must be between 60 and 3600 seconds")
            .When(x => x.IdleThresholdSeconds.HasValue);

        RuleFor(x => x.RetentionDays)
            .Must(BeValidRetentionDays)
            .WithMessage("Retention days must be one of: 30, 60, 90, 180, 365")
            .When(x => x.RetentionDays.HasValue);

        RuleFor(x => x.FocusMode)
            .SetValidator(new FocusModeDtoValidator()!)
            .When(x => x.FocusMode is not null);
    }

    private static bool BeValidAppExclusions(List<string>? exclusions)
    {
        if (exclusions == null) return true;
        return exclusions.All(e => !string.IsNullOrWhiteSpace(e));
    }

    private static bool BeValidRetentionDays(int? days)
    {
        if (!days.HasValue) return true;
        return ValidRetentionDays.Contains(days.Value);
    }
}
