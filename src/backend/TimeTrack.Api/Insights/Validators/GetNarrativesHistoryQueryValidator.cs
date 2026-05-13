using FluentValidation;
using TimeTrack.Api.Insights.Queries;

namespace TimeTrack.Api.Insights.Validators;

public sealed class GetNarrativesHistoryQueryValidator : AbstractValidator<GetNarrativesHistoryQuery>
{
    public GetNarrativesHistoryQueryValidator()
    {
        RuleFor(x => x.Limit)
            .InclusiveBetween(1, 100)
            .WithMessage("Limit must be between 1 and 100");
    }
}
