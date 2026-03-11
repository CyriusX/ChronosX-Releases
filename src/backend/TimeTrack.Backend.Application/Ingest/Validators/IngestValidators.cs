using FluentValidation;
using TimeTrack.Backend.Application.Ingest.DTOs;

namespace TimeTrack.Backend.Application.Ingest.Validators;

public sealed class ActivitySessionIngestRequestValidator : AbstractValidator<ActivitySessionIngestRequest>
{
    private const int MaxBatchSize = 100;

    public ActivitySessionIngestRequestValidator()
    {
        RuleFor(x => x.Items)
            .NotNull().WithMessage("Items are required")
            .Must(items => items.Count() <= MaxBatchSize)
            .WithMessage($"Batch size cannot exceed {MaxBatchSize} items");

        RuleForEach(x => x.Items)
            .SetValidator(new ActivitySessionItemValidator());
    }
}

public sealed class ActivitySessionItemValidator : AbstractValidator<ActivitySessionItem>
{
    public ActivitySessionItemValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id is required");

        RuleFor(x => x.ProcessName)
            .NotEmpty().WithMessage("ProcessName is required")
            .MaximumLength(255).WithMessage("ProcessName cannot exceed 255 characters");

        RuleFor(x => x.WindowTitle)
            .MaximumLength(500).WithMessage("WindowTitle cannot exceed 500 characters");

        RuleFor(x => x.AppCategory)
            .MaximumLength(100).WithMessage("AppCategory cannot exceed 100 characters");

        RuleFor(x => x.StartedAt)
            .NotEmpty().WithMessage("StartedAt is required")
            .LessThan(x => x.EndedAt).WithMessage("StartedAt must be before EndedAt");

        RuleFor(x => x.EndedAt)
            .NotEmpty().WithMessage("EndedAt is required");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty().WithMessage("IdempotencyKey is required")
            .MaximumLength(100).WithMessage("IdempotencyKey cannot exceed 100 characters");
    }
}

public sealed class IdlePeriodIngestRequestValidator : AbstractValidator<IdlePeriodIngestRequest>
{
    private const int MaxBatchSize = 100;

    public IdlePeriodIngestRequestValidator()
    {
        RuleFor(x => x.Items)
            .NotNull().WithMessage("Items are required")
            .Must(items => items.Count() <= MaxBatchSize)
            .WithMessage($"Batch size cannot exceed {MaxBatchSize} items");

        RuleForEach(x => x.Items)
            .SetValidator(new IdlePeriodItemValidator());
    }
}

public sealed class IdlePeriodItemValidator : AbstractValidator<IdlePeriodItem>
{
    public IdlePeriodItemValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id is required");

        RuleFor(x => x.StartedAt)
            .NotEmpty().WithMessage("StartedAt is required")
            .LessThan(x => x.EndedAt).WithMessage("StartedAt must be before EndedAt");

        RuleFor(x => x.EndedAt)
            .NotEmpty().WithMessage("EndedAt is required");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty().WithMessage("IdempotencyKey is required")
            .MaximumLength(100).WithMessage("IdempotencyKey cannot exceed 100 characters");
    }
}
