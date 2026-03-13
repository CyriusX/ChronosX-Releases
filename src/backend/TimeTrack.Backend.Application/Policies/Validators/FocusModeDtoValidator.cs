using FluentValidation;
using System.Collections.Generic;
using TimeTrack.Backend.Application.Policies.DTOs;

namespace TimeTrack.Backend.Application.Policies.Validators;

/// <summary>
/// Validator for FocusModeDto
/// SRP: Apenas valida configuração de modo de foco
/// </summary>
public sealed class FocusModeDtoValidator : AbstractValidator<FocusModeDto>
{
    private static readonly HashSet<string> ValidModes = new(StringComparer.OrdinalIgnoreCase)
    {
        "pomodoro", "ultradian", "none"
    };

    public FocusModeDtoValidator()
    {
        RuleFor(x => x.Mode)
            .Must(BeValidMode)
            .WithMessage("Mode must be one of: pomodoro, ultradian, none");

        RuleFor(x => x.Mode)
            .NotEqual("none")
            .WithMessage("Mode cannot be 'none' when focus mode is enabled")
            .When(x => x.Enabled);

        RuleFor(x => x.Pomodoro)
            .SetValidator(new PomodoroConfigDtoValidator()!)
            .When(x => x.Pomodoro is not null);

        RuleFor(x => x.Ultradian)
            .SetValidator(new UltradianConfigDtoValidator()!)
            .When(x => x.Ultradian is not null);
    }

    private static bool BeValidMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode)) return true;
        return ValidModes.Contains(mode);
    }
}
