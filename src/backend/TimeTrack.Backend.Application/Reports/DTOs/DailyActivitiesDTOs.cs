using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MediatR;

namespace TimeTrack.Backend.Application.Reports.DTOs;

/// <summary>
/// Query to get detailed activity sessions for a specific date.
/// Returns individual sessions (not aggregated) for timeline display.
/// </summary>
public sealed record DailyActivitiesQuery(
    [Required]
    Guid? UserId,

    [Required]
    DateTime Date,

    string? Timezone = null
) : IRequest<DailyActivitiesResponse>;

/// <summary>
/// Response containing individual activity sessions for a day
/// </summary>
public sealed class DailyActivitiesResponse
{
    [JsonPropertyName("date")]
    public string Date { get; init; } = string.Empty;

    [JsonPropertyName("sessions")]
    public List<ActivitySessionDto> Sessions { get; init; } = [];
}

/// <summary>
/// Individual activity session with timestamps and window title
/// </summary>
public sealed class ActivitySessionDto
{
    [JsonPropertyName("processName")]
    public string ProcessName { get; init; } = string.Empty;

    [JsonPropertyName("windowTitle")]
    public string? WindowTitle { get; init; }

    [JsonPropertyName("appCategory")]
    public string? AppCategory { get; init; }

    [JsonPropertyName("startedAt")]
    public DateTime StartedAt { get; init; }

    [JsonPropertyName("endedAt")]
    public DateTime EndedAt { get; init; }

    [JsonPropertyName("durationSeconds")]
    public int DurationSeconds { get; init; }
}
