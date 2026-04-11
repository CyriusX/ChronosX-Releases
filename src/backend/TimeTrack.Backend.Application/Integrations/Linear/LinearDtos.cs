namespace TimeTrack.Backend.Application.Integrations.Linear;

/// <summary>
/// Plain C# records that mirror the shape of Linear's GraphQL payloads.
/// Only the fields TimeTrack actually consumes are modeled — everything else
/// is dropped by the deserializer.
/// </summary>

public sealed record LinearViewer(
    string Id,
    string Name,
    string? Email);

public sealed record LinearProject(
    string Id,
    string Name,
    string? Color,
    string? State,
    string? Description);

public sealed record LinearWorkflowState(
    string Id,
    string Name,
    /// <summary>One of: triage, backlog, unstarted, started, completed, canceled.</summary>
    string Type,
    double Position);

public sealed record LinearTeamStates(
    string TeamId,
    IReadOnlyList<LinearWorkflowState> States);

public sealed record LinearIssue(
    string Id,
    string Identifier,
    string Title,
    string? Description,
    /// <summary>Linear: 0=None, 1=Urgent, 2=High, 3=Medium, 4=Low.</summary>
    int Priority,
    DateTime? DueDate,
    string? Url,
    LinearIssueState State,
    LinearIssueProject? Project,
    LinearIssueTeam Team);

public sealed record LinearIssueState(
    string Id,
    string Name,
    string Type);

public sealed record LinearIssueProject(
    string Id,
    string Name);

public sealed record LinearIssueTeam(
    string Id,
    string? Key);
