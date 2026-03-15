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

public sealed class FocusSessionIngestRequestValidator : AbstractValidator<FocusSessionIngestRequest>
{
    private const int MaxBatchSize = 100;

    public FocusSessionIngestRequestValidator()
    {
        RuleFor(x => x.Items)
            .NotNull().WithMessage("Items are required")
            .Must(items => items.Count() <= MaxBatchSize)
            .WithMessage($"Batch size cannot exceed {MaxBatchSize} items");

        RuleForEach(x => x.Items)
            .SetValidator(new FocusSessionItemValidator());
    }
}

public sealed class FocusSessionItemValidator : AbstractValidator<FocusSessionItem>
{
    private static readonly string[] ValidStatuses = { "InProgress", "Completed", "Cancelled" };

    public FocusSessionItemValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Id is required");

        RuleFor(x => x.StartedAt)
            .NotEmpty().WithMessage("StartedAt is required");

        RuleFor(x => x.PlannedDurationMinutes)
            .GreaterThan(0).WithMessage("PlannedDurationMinutes must be greater than 0")
            .LessThanOrEqualTo(480).WithMessage("PlannedDurationMinutes cannot exceed 480 minutes (8 hours)");

        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required")
            .Must(s => ValidStatuses.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Status must be one of: InProgress, Completed, Cancelled");

        RuleFor(x => x.ActualDurationMinutes)
            .GreaterThan(0).WithMessage("ActualDurationMinutes must be greater than 0 when provided")
            .LessThanOrEqualTo(480).WithMessage("ActualDurationMinutes cannot exceed 480 minutes (8 hours)")
            .When(x => x.ActualDurationMinutes.HasValue);

        RuleFor(x => x.FocusScore)
            .GreaterThanOrEqualTo(0).WithMessage("FocusScore must be between 0 and 100")
            .LessThanOrEqualTo(100).WithMessage("FocusScore must be between 0 and 100")
            .When(x => x.FocusScore.HasValue);

        RuleFor(x => x.EndedAt)
            .NotNull().WithMessage("EndedAt is required when status is Completed or Cancelled")
            .GreaterThanOrEqualTo(x => x.StartedAt).WithMessage("EndedAt must be after StartedAt")
            .When(x => x.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase) ||
                       x.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase));

        RuleFor(x => x.FocusScore)
            .NotNull().WithMessage("FocusScore is required when status is Completed")
            .When(x => x.Status.Equals("Completed", StringComparison.OrdinalIgnoreCase));

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty().WithMessage("IdempotencyKey is required")
            .MaximumLength(64).WithMessage("IdempotencyKey cannot exceed 64 characters");
    }
}
