namespace TimeTrack.Backend.Domain.ValueObjects;

public enum SubscriptionStatus
{
    None = 0,
    Active = 1,
    Trialing = 2,
    PastDue = 3,
    Canceled = 4,
    Unpaid = 5,
    Incomplete = 6
}
