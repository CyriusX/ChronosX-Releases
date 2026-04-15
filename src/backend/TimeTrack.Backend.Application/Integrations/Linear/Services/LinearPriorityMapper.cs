using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.Integrations.Linear.Services;

/// <summary>
/// Linear priority scale: 0=None, 1=Urgent, 2=High, 3=Medium, 4=Low.
/// Our TaskPriority enum uses the same values with different ordering:
/// None=0, Low=1, Medium=2, High=3, Urgent=4. Hence the table.
/// </summary>
public static class LinearPriorityMapper
{
    public static TaskPriority FromLinear(int linearPriority) => linearPriority switch
    {
        1 => TaskPriority.Urgent,
        2 => TaskPriority.High,
        3 => TaskPriority.Medium,
        4 => TaskPriority.Low,
        _ => TaskPriority.None
    };

    public static int ToLinear(TaskPriority priority) => priority switch
    {
        TaskPriority.Urgent => 1,
        TaskPriority.High => 2,
        TaskPriority.Medium => 3,
        TaskPriority.Low => 4,
        _ => 0
    };
}
