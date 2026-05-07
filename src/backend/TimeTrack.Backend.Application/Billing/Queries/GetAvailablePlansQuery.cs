using MediatR;
using TimeTrack.Backend.Application.Billing.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Billing.Queries;

public sealed record GetAvailablePlansQuery : IRequest<IReadOnlyList<PlanResponse>>;

public sealed class GetAvailablePlansQueryHandler : IRequestHandler<GetAvailablePlansQuery, IReadOnlyList<PlanResponse>>
{
    private readonly ISubscriptionPlanRepository _planRepository;

    public GetAvailablePlansQueryHandler(ISubscriptionPlanRepository planRepository)
    {
        _planRepository = planRepository;
    }

    public async Task<IReadOnlyList<PlanResponse>> Handle(GetAvailablePlansQuery request, CancellationToken cancellationToken)
    {
        var plans = await _planRepository.GetAllAsync(cancellationToken);

        return plans.Select(p => new PlanResponse
        {
            Id = p.Id,
            Name = p.Name,
            Tier = p.Tier.ToString().ToLowerInvariant(),
            MonthlyPriceCents = p.MonthlyPriceCents,
            YearlyPriceCents = p.YearlyPriceCents,
            MaxUsers = p.MaxUsers,
            MaxDevices = p.MaxDevices,
            StripePriceId = p.StripePriceId ?? string.Empty,
            Features = new PlanFeatureSet
            {
                Flags = p.ToFeatureDictionary()
            }
        }).ToList();
    }
}
