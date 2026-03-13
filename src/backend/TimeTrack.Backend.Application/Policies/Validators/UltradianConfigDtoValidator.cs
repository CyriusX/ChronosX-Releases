using FluentValidation;
using TimeTrack.Backend.Application.Policies.DTOs;

namespace TimeTrack.Backend.Application.Policies.Validators;

/// <summary>
/// Validator for UltradianConfigDto
/// SRP: Apenas valida configuração do ciclo Ultradian
/// </summary>
public sealed class UltradianConfigDtoValidator : AbstractValidator<UltradianConfigDto>
{
    public UltradianConfigDtoValidator()
    {
        RuleFor(x => x.FocusMinutes)
            .InclusiveBetween(10, 180)
            .WithMessage("Ultradian focus minutes must be between 10 and 180");

        RuleFor(x => x.BreakMinutes)
            .InclusiveBetween(5, 60)
            .WithMessage("Ultradian break minutes must be between 5 and 60");
    }
}
