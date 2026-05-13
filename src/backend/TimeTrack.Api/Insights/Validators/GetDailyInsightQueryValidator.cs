using FluentValidation;
using TimeTrack.Api.Insights.Queries;

namespace TimeTrack.Api.Insights.Validators;

public sealed class GetDailyInsightQueryValidator : AbstractValidator<GetDailyInsightQuery>
{
    public GetDailyInsightQueryValidator()
    {
        When(x => !string.IsNullOrEmpty(x.Date), () =>
        {
            RuleFor(x => x.Date!)
                .Must(BeValidDate).WithMessage("Date must be a valid date in yyyy-MM-dd format")
                .Must(NotBeInFuture).WithMessage("Date cannot be in the future");
        });
    }

    private static bool BeValidDate(string? date) => DateOnly.TryParse(date, out _);
    private static bool NotBeInFuture(string? date) =>
        DateOnly.TryParse(date, out var d) && d <= DateOnly.FromDateTime(DateTime.UtcNow);
}
