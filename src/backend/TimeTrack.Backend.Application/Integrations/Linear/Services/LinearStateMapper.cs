using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.Integrations.Linear.Services;

/// <summary>
/// Bidirectional mapping between Linear's workflow state taxonomy and the
/// kanban's four-column enum.
/// </summary>
public static class LinearStateMapper
{
    /// <summary>
    /// Map a Linear state (by name + type) to our kanban status.
    /// Priority: if the state's name contains "review" (case-insensitive)
    /// and the type is "started", treat as InReview. Otherwise map purely
    /// on type.
    /// </summary>
    public static ProjectTaskStatus FromLinear(string stateName, string stateType)
    {
        var normalizedType = (stateType ?? string.Empty).Trim().ToLowerInvariant();
        var normalizedName = (stateName ?? string.Empty).Trim().ToLowerInvariant();

        if (normalizedType == "started" && normalizedName.Contains("review"))
            return ProjectTaskStatus.InReview;

        return normalizedType switch
        {
            "triage" or "backlog" or "unstarted" => ProjectTaskStatus.Todo,
            "started" => ProjectTaskStatus.InProgress,
            "completed" => ProjectTaskStatus.Done,
            _ => ProjectTaskStatus.Todo
        };
    }

    /// <summary>
    /// Resolve the most appropriate Linear workflow state for a local status,
    /// given the list of available states for the issue's team.
    /// Returns null if no plausible match exists (extremely rare — every Linear
    /// team has at least one state of each core type).
    /// </summary>
    public static LinearWorkflowState? ResolveLinearState(
        ProjectTaskStatus status,
        IReadOnlyList<LinearWorkflowState> teamStates)
    {
        if (teamStates is null || teamStates.Count == 0)
            return null;

        LinearWorkflowState? FirstByType(params string[] types)
        {
            foreach (var t in types)
            {
                var match = teamStates
                    .Where(s => string.Equals(s.Type, t, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(s => s.Position)
                    .FirstOrDefault();
                if (match is not null) return match;
            }
            return null;
        }

        LinearWorkflowState? FirstStartedMatching(Func<LinearWorkflowState, bool> predicate)
        {
            return teamStates
                .Where(s => string.Equals(s.Type, "started", StringComparison.OrdinalIgnoreCase))
                .Where(predicate)
                .OrderBy(s => s.Position)
                .FirstOrDefault();
        }

        return status switch
        {
            ProjectTaskStatus.Todo => FirstByType("unstarted", "backlog", "triage"),
            ProjectTaskStatus.InProgress => FirstStartedMatching(s => !s.Name.Contains("review", StringComparison.OrdinalIgnoreCase))
                                            ?? FirstByType("started"),
            ProjectTaskStatus.InReview => FirstStartedMatching(s => s.Name.Contains("review", StringComparison.OrdinalIgnoreCase))
                                           ?? FirstByType("started"),
            ProjectTaskStatus.Done => FirstByType("completed"),
            _ => null
        };
    }
}
