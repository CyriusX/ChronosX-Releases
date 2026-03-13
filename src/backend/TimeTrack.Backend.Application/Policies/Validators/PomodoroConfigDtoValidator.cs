using FluentValidation;
using TimeTrack.Backend.Application.Policies.DTOs;

namespace TimeTrack.Backend.Application.Policies.Validators;

/// <summary>
/// Validator for PomodoroConfigDto
/// SRP: Apenas valida configuração do Pomodoro
/// </summary>
public sealed class PomodoroConfigDtoValidator : AbstractValidator<PomodoroConfigDto>
{
    public PomodoroConfigDtoValidator()
    {
        RuleFor(x => x.FocusMinutes)
            .InclusiveBetween(10, 180)
            .WithMessage("Pomodoro focus minutes must be between 10 and 180");

        RuleFor(x => x.ShortBreakMinutes)
            .InclusiveBetween(5, 60)
            .WithMessage("Pomodoro short break minutes must be between 5 and 60");

        RuleFor(x => x.LongBreakMinutes)
            .InclusiveBetween(5, 60)
            .WithMessage("Pomodoro long break minutes must be between 5 and 60");

        RuleFor(x => x.CyclesBeforeLongBreak)
            .InclusiveBetween(2, 8)
            .WithMessage("Pomodoro cycles before long break must be between 2 and 8");
    }
}
