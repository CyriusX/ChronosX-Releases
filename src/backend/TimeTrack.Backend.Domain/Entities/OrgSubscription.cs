using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Domain.Entities;

public sealed class OrgSubscription
{
    public Guid Id { get; private set; }
    public Guid OrgId { get; private set; }
    public Guid? PlanId { get; private set; }
    public string? StripeCustomerId { get; private set; }
    public string? StripeSubscriptionId { get; private set; }
    public SubscriptionStatus Status { get; private set; }
    public DateTime? CurrentPeriodStart { get; private set; }
    public DateTime? CurrentPeriodEnd { get; private set; }
    public DateTime? TrialEnd { get; private set; }
    public DateTime? GracePeriodEnd { get; private set; }
    public DateTime? CanceledAt { get; private set; }
    public bool CancelAtPeriodEnd { get; private set; }
    public int Quantity { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public Organization? Organization { get; private set; }
    public SubscriptionPlan? Plan { get; private set; }

    private OrgSubscription() { }

    public static OrgSubscription Create(Guid orgId)
    {
        return new OrgSubscription
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Status = SubscriptionStatus.None,
            Quantity = 0,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Activate(Guid planId, string stripeSubscriptionId, string stripeCustomerId,
        DateTime periodStart, DateTime periodEnd, int quantity = 1)
    {
        PlanId = planId;
        StripeSubscriptionId = stripeSubscriptionId;
        StripeCustomerId = stripeCustomerId;
        Status = SubscriptionStatus.Active;
        CurrentPeriodStart = periodStart;
        CurrentPeriodEnd = periodEnd;
        Quantity = quantity;
        CanceledAt = null;
        CancelAtPeriodEnd = false;
        GracePeriodEnd = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void EnterTrial(Guid planId, DateTime trialEnd, int quantity = 1)
    {
        PlanId = planId;
        Status = SubscriptionStatus.Trialing;
        TrialEnd = trialEnd;
        Quantity = quantity;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkPastDue(int gracePeriodDays)
    {
        Status = SubscriptionStatus.PastDue;
        GracePeriodEnd = DateTime.UtcNow.AddDays(gracePeriodDays);
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkUnpaid()
    {
        Status = SubscriptionStatus.Unpaid;
        GracePeriodEnd = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel(bool atPeriodEnd)
    {
        if (atPeriodEnd)
        {
            CancelAtPeriodEnd = true;
        }
        else
        {
            Status = SubscriptionStatus.Canceled;
        }
        CanceledAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reactivate(Guid planId, DateTime periodStart, DateTime periodEnd, int quantity = 1)
    {
        PlanId = planId;
        Status = SubscriptionStatus.Active;
        CurrentPeriodStart = periodStart;
        CurrentPeriodEnd = periodEnd;
        Quantity = quantity;
        CanceledAt = null;
        CancelAtPeriodEnd = false;
        GracePeriodEnd = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdatePeriod(DateTime periodStart, DateTime periodEnd)
    {
        CurrentPeriodStart = periodStart;
        CurrentPeriodEnd = periodEnd;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateQuantity(int quantity)
    {
        Quantity = quantity;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool HasSeatAvailable(int currentUsers) =>
        HasActiveAccess() && Quantity > currentUsers;

    public void ChangePlan(Guid newPlanId)
    {
        PlanId = newPlanId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetStatus(SubscriptionStatus status)
    {
        Status = status;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsInGracePeriod()
    {
        return Status == SubscriptionStatus.PastDue
            && GracePeriodEnd.HasValue
            && GracePeriodEnd.Value > DateTime.UtcNow;
    }

    /// <summary>
    /// True when the subscription is currently in its trial window.
    /// A Trialing status with a past TrialEnd is treated as expired so users
    /// can't keep trialing forever if the SubscriptionStatusJob hasn't run yet.
    /// </summary>
    public bool IsTrialActive()
    {
        return Status == SubscriptionStatus.Trialing
            && (TrialEnd == null || TrialEnd.Value > DateTime.UtcNow);
    }

    public bool HasActiveAccess()
    {
        return Status == SubscriptionStatus.Active
            || IsTrialActive()
            || IsInGracePeriod();
    }

    /// <summary>
    /// Transitions a Trialing subscription whose TrialEnd has passed into
    /// Canceled state, so the paywall shows and the user must pick a plan.
    /// No-op if the subscription isn't a trial or the trial hasn't ended yet.
    /// </summary>
    public void ExpireTrial()
    {
        if (Status != SubscriptionStatus.Trialing) return;
        if (TrialEnd == null || TrialEnd.Value > DateTime.UtcNow) return;

        Status = SubscriptionStatus.Canceled;
        CanceledAt = TrialEnd;
        UpdatedAt = DateTime.UtcNow;
    }
}
