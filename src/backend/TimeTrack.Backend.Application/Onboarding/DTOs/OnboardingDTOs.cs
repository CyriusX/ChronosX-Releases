namespace TimeTrack.Backend.Application.Onboarding.DTOs;

public sealed class OnboardingStatusResponse
{
    public bool IsComplete { get; init; }
    public Guid OrgId { get; init; }
}

public sealed class CompleteOnboardingResponse
{
    public bool Success { get; init; }
}
