using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TimeTrack.Backend.Application.Billing.DTOs;
using TimeTrack.Backend.Application.Billing.Queries;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Persistence;
using TimeTrack.Backend.Infrastructure.Repositories;
using Xunit;

namespace TimeTrack.Backend.Tests.Billing;

public sealed class GetAvailablePlansQueryHandlerTests : IDisposable
{
    private readonly TimeTrackDbContext _db;
    private readonly ISubscriptionPlanRepository _planRepository;
    private readonly Mock<IPaymentGatewayService> _stripeMock;
    private readonly ILogger<GetAvailablePlansQueryHandler> _logger;

    public GetAvailablePlansQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<TimeTrackDbContext>()
            .UseInMemoryDatabase($"PlansTests_{Guid.NewGuid()}")
            .Options;

        _db = new TimeTrackDbContext(options);
        _planRepository = new SubscriptionPlanRepository(_db);
        _stripeMock = new Mock<IPaymentGatewayService>();
        _logger = LoggerFactory.Create(_ => { }).CreateLogger<GetAvailablePlansQueryHandler>();
    }

    public void Dispose() => _db.Dispose();

    private GetAvailablePlansQueryHandler CreateHandler() =>
        new(_stripeMock.Object, _planRepository, _logger);

    private static StripePlanInfo ProPlan() => new()
    {
        StripeProductId = "prod_test_pro",
        StripePriceId = "price_test_pro",
        Name = "Pro",
        Tier = "pro",
        MonthlyPriceCents = 1200,
        MaxUsers = 25,
        MaxDevices = 50,
        Features = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            ["machineMonitoring"] = true,
            ["advancedReports"] = true,
            ["focusMode"] = true,
            ["apiAccess"] = true,
            ["customCategories"] = true,
            ["linearIntegration"] = true,
        }
    };

    private static StripePlanInfo EnterprisePlan() => new()
    {
        StripeProductId = "prod_test_ent",
        StripePriceId = "price_test_ent",
        Name = "Enterprise",
        Tier = "enterprise",
        MonthlyPriceCents = 10000,
        MaxUsers = 0,
        MaxDevices = 0,
        Features = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            ["machineMonitoring"] = true,
            ["advancedReports"] = true,
            ["focusMode"] = true,
            ["apiAccess"] = true,
            ["prioritySupport"] = true,
            ["billingAnalytics"] = true,
            ["customCategories"] = true,
            ["linearIntegration"] = true,
        }
    };

    // ========================================================================
    // Happy path
    // ========================================================================

    [Fact]
    public async Task Handle_ReturnsFreePlusStripePlans()
    {
        _stripeMock.Setup(s => s.ListPlansAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([ProPlan(), EnterprisePlan()]);

        var result = await CreateHandler().Handle(new GetAvailablePlansQuery(), CancellationToken.None);

        result.Should().HaveCount(3);
        result.Select(p => p.Tier).Should().BeEquivalentTo(["free", "pro", "enterprise"]);
    }

    [Fact]
    public async Task Handle_CreatesFreePlan_WithCorrectFeatures()
    {
        _stripeMock.Setup(s => s.ListPlansAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([ProPlan()]);

        var result = await CreateHandler().Handle(new GetAvailablePlansQuery(), CancellationToken.None);
        var free = result.First(p => p.Tier == "free");

        free.MonthlyPriceCents.Should().Be(0);
        free.MaxUsers.Should().Be(3);
        free.MaxDevices.Should().Be(3);
        free.Features.Flags["MachineMonitoring"].Should().BeTrue();
        free.Features.Flags["FocusMode"].Should().BeTrue();
        free.Features.Flags["AdvancedReports"].Should().BeFalse();
        free.Features.Flags["ApiAccess"].Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ProPlan_HasStripePriceIdAndCorrectData()
    {
        var pro = ProPlan();
        _stripeMock.Setup(s => s.ListPlansAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([pro]);

        var result = await CreateHandler().Handle(new GetAvailablePlansQuery(), CancellationToken.None);
        var proResult = result.First(p => p.Tier == "pro");

        proResult.StripePriceId.Should().Be(pro.StripePriceId);
        proResult.MonthlyPriceCents.Should().Be(1200);
        proResult.MaxUsers.Should().Be(25);
        proResult.MaxDevices.Should().Be(50);
        proResult.Features.Flags["machineMonitoring"].Should().BeTrue();
    }

    // ========================================================================
    // DB sync
    // ========================================================================

    [Fact]
    public async Task Handle_DoesNotDuplicateFreePlan_OnRepeatedCalls()
    {
        _stripeMock.Setup(s => s.ListPlansAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([ProPlan()]);

        var handler = CreateHandler();
        await handler.Handle(new GetAvailablePlansQuery(), CancellationToken.None);
        await handler.Handle(new GetAvailablePlansQuery(), CancellationToken.None);
        await handler.Handle(new GetAvailablePlansQuery(), CancellationToken.None);

        var allPlans = await _planRepository.GetAllAsync();
        allPlans.Count(p => p.Tier == Domain.ValueObjects.PlanTier.Free).Should().Be(1);
    }

    [Fact]
    public async Task Handle_DoesNotDuplicateStripePlans_OnRepeatedCalls()
    {
        _stripeMock.Setup(s => s.ListPlansAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([ProPlan(), EnterprisePlan()]);

        var handler = CreateHandler();
        await handler.Handle(new GetAvailablePlansQuery(), CancellationToken.None);
        await handler.Handle(new GetAvailablePlansQuery(), CancellationToken.None);

        var allPlans = await _planRepository.GetAllAsync();
        allPlans.Should().HaveCount(3, "Free + Pro + Enterprise, no duplicates");
    }

    [Fact]
    public async Task Handle_ReturnsStablePlanIds_OnRepeatedCalls()
    {
        _stripeMock.Setup(s => s.ListPlansAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([ProPlan()]);

        var handler = CreateHandler();
        var first = await handler.Handle(new GetAvailablePlansQuery(), CancellationToken.None);
        var second = await handler.Handle(new GetAvailablePlansQuery(), CancellationToken.None);

        var firstIds = first.Select(p => p.Id).OrderBy(id => id).ToList();
        var secondIds = second.Select(p => p.Id).OrderBy(id => id).ToList();

        firstIds.Should().BeEquivalentTo(secondIds);
    }

    // ========================================================================
    // Fallback
    // ========================================================================

    [Fact]
    public async Task Handle_FallsBackToDb_WhenStripeFails()
    {
        _stripeMock.Setup(s => s.ListPlansAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Stripe is down"));

        var result = await CreateHandler().Handle(new GetAvailablePlansQuery(), CancellationToken.None);

        result.Should().NotBeEmpty("Should return Free plan from DB even when Stripe is down");
        result.Should().ContainSingle(p => p.Tier == "free");
    }

    [Fact]
    public async Task Handle_FallsBackToDb_WithPreviouslySyncedPlans()
    {
        // First call: Stripe works and syncs Pro
        _stripeMock.Setup(s => s.ListPlansAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([ProPlan()]);
        var handler = CreateHandler();
        await handler.Handle(new GetAvailablePlansQuery(), CancellationToken.None);

        // Second call: Stripe fails — should still return Pro from DB
        _stripeMock.Setup(s => s.ListPlansAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Stripe is down"));

        var result = await handler.Handle(new GetAvailablePlansQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(p => p.Tier).Should().Contain(["free", "pro"]);
    }

    // ========================================================================
    // Edge cases
    // ========================================================================

    [Fact]
    public async Task Handle_SkipsStripePlan_WithUnrecognizedTier()
    {
        var invalidPlan = new StripePlanInfo
        {
            StripeProductId = "prod_test",
            StripePriceId = "price_test",
            Name = "Unknown",
            Tier = "startup",
            MonthlyPriceCents = 500,
            MaxUsers = 10,
            MaxDevices = 20,
            Features = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        };

        _stripeMock.Setup(s => s.ListPlansAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([ProPlan(), invalidPlan]);

        var result = await CreateHandler().Handle(new GetAvailablePlansQuery(), CancellationToken.None);

        result.Should().HaveCount(2, "Free + Pro only — 'startup' tier is skipped");
        result.Should().NotContain(p => p.Tier == "startup");
    }

    [Fact]
    public async Task Handle_ReturnsOnlyFreePlan_WhenStripeReturnsEmpty()
    {
        _stripeMock.Setup(s => s.ListPlansAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await CreateHandler().Handle(new GetAvailablePlansQuery(), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Tier.Should().Be("free");
    }

    [Fact]
    public async Task Handle_UpdatesExistingPlan_WhenStripeDataChanges()
    {
        _stripeMock.Setup(s => s.ListPlansAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([ProPlan()]);

        var handler = CreateHandler();
        var first = await handler.Handle(new GetAvailablePlansQuery(), CancellationToken.None);
        var proFirst = first.First(p => p.Tier == "pro");
        var proId = proFirst.Id;

        // Stripe returns updated price
        var updatedPro = ProPlan() with { MonthlyPriceCents = 1500, Name = "Pro Plus" };
        _stripeMock.Setup(s => s.ListPlansAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([updatedPro]);

        var second = await handler.Handle(new GetAvailablePlansQuery(), CancellationToken.None);
        var proSecond = second.First(p => p.Tier == "pro");

        proSecond.Id.Should().Be(proId, "Same plan updated, not duplicated");
        proSecond.MonthlyPriceCents.Should().Be(1500);
        proSecond.Name.Should().Be("Pro Plus");
    }
}
