namespace TimeTrack.Backend.Domain.ValueObjects;

public enum AgentNotificationKind
{
    Generic = 1,
    TaskAssigned = 2,
    TaskUnassigned = 3,
    TaskUpdated = 4,
    ProjectMembershipChanged = 5,
    DeadlineToday = 6,
    AiInsight = 7,
}
