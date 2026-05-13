using System.Globalization;
using FluentValidation;
using TimeTrack.Api.Insights.Queries;

namespace TimeTrack.Api.Insights.Validators;

public sealed class GetReportsInsightsQueryValidator : AbstractValidator<GetReportsInsightsQuery>
{
    public GetReportsInsightsQueryValidator()
    {
        When(x => !string.IsNullOrEmpty(x.StartDate) && !string.IsNullOrEmpty(x.EndDate), () =>
        {
            RuleFor(x => x)
                .Must(BeValidDateRange).WithMessage("Start date must be before or equal to end date")
                .Must(BeWithinMaxRange).WithMessage("Date range cannot exceed 90 days");
        });

        When(x => !string.IsNullOrEmpty(x.StartDate), () =>
        {
            RuleFor(x => x.StartDate!).Must(BeValidDate).WithMessage("StartDate must be a valid date");
        });

        When(x => !string.IsNullOrEmpty(x.EndDate), () =>
        {
            RuleFor(x => x.EndDate!).Must(BeValidDate).WithMessage("EndDate must be a valid date");
        });
    }

    private static bool BeValidDate(string? date) => DateOnly.TryParse(date, CultureInfo.InvariantCulture, out _);

    private static bool BeValidDateRange(GetReportsInsightsQuery query)
    {
        if (!DateOnly.TryParse(query.StartDate, CultureInfo.InvariantCulture, out var start)) return false;
        if (!DateOnly.TryParse(query.EndDate, CultureInfo.InvariantCulture, out var end)) return false;
        return start <= end;
    }

    private static bool BeWithinMaxRange(GetReportsInsightsQuery query)
    {
        if (!DateOnly.TryParse(query.StartDate, CultureInfo.InvariantCulture, out var start)) return false;
        if (!DateOnly.TryParse(query.EndDate, CultureInfo.InvariantCulture, out var end)) return false;
        return (end.DayNumber - start.DayNumber) <= 90;
    }
}
