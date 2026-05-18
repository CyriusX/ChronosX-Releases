using System.Text.Json;

namespace TimeTrack.Backend.Domain.ValueObjects;

/// <summary>
/// Intermediate result from pattern detection methods on UserPattern.
/// Contains the data needed to create or update a UserPattern entity.
/// </summary>
public sealed record PatternCandidate(
    string Tag,
    double Strength,
    JsonElement Evidence);
