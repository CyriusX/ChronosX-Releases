namespace TimeTrack.Backend.Domain.ValueObjects;

/// <summary>
/// A detected live insight condition ready to be persisted as a SmartAlert.
/// </summary>
public sealed record LiveInsightCandidate(
    Guid UserId,
    Guid OrgId,
    string AlertType,
    string Severity,
    string Message,
    double Priority);
