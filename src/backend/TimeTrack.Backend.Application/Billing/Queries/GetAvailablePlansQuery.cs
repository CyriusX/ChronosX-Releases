using MediatR;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Application.Billing.DTOs;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.Billing.Queries;

public sealed record GetAvailablePlansQuery : IRequest<IReadOnlyList<PlanResponse>>;

public sealed class GetAvailablePlansQueryHandler : IRequestHandler<GetAvailablePlansQuery, IReadOnlyList<PlanResponse>>
{
    private static readonly SemaphoreSlim _syncLock = new(1, 1);

    private readonly IPaymentGatewayService _paymentGateway;
    private readonly ISubscriptionPlanRepository _planRepository;
    private readonly ILogger<GetAvailablePlansQueryHandler> _logger;

    public GetAvailablePlansQueryHandler(
        IPaymentGatewayService paymentGateway,
        ISubscriptionPlanRepository planRepository,
        ILogger<GetAvailablePlansQueryHandler> logger)
    {
        _paymentGateway = paymentGateway;
        _planRepository = planRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PlanResponse>> Handle(GetAvailablePlansQuery request, CancellationToken cancellationToken)
    {
        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            await EnsureFreePlanAsync(cancellationToken);

            IReadOnlyList<StripePlanInfo> stripePlans;
            try
            {
                stripePlans = await _paymentGateway.ListPlansAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch plans from Stripe — falling back to DB only");
                return await BuildResponseFromDbAsync(cancellationToken);
            }

            foreach (var sp in stripePlans)
            {
                await SyncStripePlanToDbAsync(sp, cancellationToken);
            }

            return await BuildResponseFromDbAsync(cancellationToken);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task EnsureFreePlanAsync(CancellationToken ct)
    {
        var existing = await _planRepository.GetByTierAsync(PlanTier.Free, ct);
        if (existing is not null) return;

        var freePlan = SubscriptionPlan.Create(
            name: "Free",
            tier: PlanTier.Free,
            monthlyPriceCents: 0,
            maxUsers: 3,
            maxDevices: 3,
            features: FreePlanFeatures());

        await _planRepository.AddAsync(freePlan, ct);
    }

    private async Task SyncStripePlanToDbAsync(StripePlanInfo stripePlan, CancellationToken ct)
    {
        if (!Enum.TryParse<PlanTier>(stripePlan.Tier, ignoreCase: true, out var tier))
        {
            _logger.LogWarning("Skipping Stripe product {ProductId}: unrecognized tier '{Tier}'",
                stripePlan.StripeProductId, stripePlan.Tier);
            return;
        }

        var existing = await _planRepository.GetByTierAsync(tier, ct);

        if (existing is not null)
        {
            existing.UpdateFromConfig(
                stripePlan.Name,
                stripePlan.MonthlyPriceCents,
                yearlyPriceCents: null,
                stripePlan.MaxUsers,
                stripePlan.MaxDevices,
                stripePlan.Features,
                stripePlan.StripePriceId,
                stripePlan.StripeProductId);

            await _planRepository.UpdateAsync(existing, ct);
        }
        else
        {
            var plan = SubscriptionPlan.Create(
                name: stripePlan.Name,
                tier: tier,
                monthlyPriceCents: stripePlan.MonthlyPriceCents,
                maxUsers: stripePlan.MaxUsers,
                maxDevices: stripePlan.MaxDevices,
                features: stripePlan.Features,
                stripePriceId: stripePlan.StripePriceId,
                stripeProductId: stripePlan.StripeProductId);

            await _planRepository.AddAsync(plan, ct);
        }
    }

    private async Task<IReadOnlyList<PlanResponse>> BuildResponseFromDbAsync(CancellationToken ct)
    {
        var plans = await _planRepository.GetAllAsync(ct);

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

    private static Dictionary<string, bool> FreePlanFeatures() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["MachineMonitoring"] = true,
        ["AdvancedReports"] = false,
        ["FocusMode"] = true,
        ["ApiAccess"] = false,
        ["PrioritySupport"] = false,
        ["CustomCategories"] = false,
        ["LinearIntegration"] = false,
        ["BillingAnalytics"] = false,
    };
}
