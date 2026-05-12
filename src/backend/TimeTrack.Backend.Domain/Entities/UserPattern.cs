using System.Text.Json;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Domain.Entities;

public sealed class UserPattern
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid OrgId { get; private set; }
    public string PatternTag { get; private set; } = string.Empty;
    public DateOnly DetectedAt { get; private set; }
    public double Strength { get; private set; }
    public JsonElement Evidence { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }

    private UserPattern() { }

    public static UserPattern Create(
        Guid userId,
        Guid orgId,
        string patternTag,
        DateOnly detectedAt,
        double strength,
        JsonElement evidence,
        string? description = null)
    {
        return new UserPattern
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrgId = orgId,
            PatternTag = patternTag,
            DetectedAt = detectedAt,
            Strength = strength,
            Evidence = evidence,
            Description = description,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void SetDescription(string description)
    {
        Description = description;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Reactivate(double strength, JsonElement evidence)
    {
        Strength = strength;
        Evidence = evidence;
        IsActive = true;
    }

    /// <summary>
    /// Detects if user is consistently productive in the morning (Mon-Fri).
    /// Requires at least 4 workdays with productivity ratio above threshold.
    /// </summary>
    public static PatternCandidate? TryDetectMorningProductive(
        List<DailyFocusScore> scores,
        double morningProductiveRatio)
    {
        var workDays = scores
            .Where(s => s.Date.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday)
            .ToList();

        if (workDays.Count < 4) return null;

        var confirmedDays = workDays.Count(s => s.ProductivityRatio > morningProductiveRatio);
        if (confirmedDays < 4) return null;

        var strength = confirmedDays / 5.0;
        var evidence = JsonSerializer.SerializeToElement(new
        {
            confirmedDays,
            totalWorkDays = workDays.Count,
            avgRatio = workDays.Average(s => s.ProductivityRatio),
        });

        return new PatternCandidate("morning_productive", strength, evidence);
    }

    /// <summary>
    /// Detects afternoon productivity drops using low focus score + low productivity ratio as proxy.
    /// </summary>
    public static PatternCandidate? TryDetectAfternoonFocusDrop(
        List<DailyFocusScore> scores,
        double afternoonFocusDropMultiplier)
    {
        if (scores.Count < 4) return null;

        var avgFocus = scores.Average(s => s.FocusScore);
        var lowFocusDays = scores.Count(s =>
            s.FocusScore < avgFocus * afternoonFocusDropMultiplier && s.ProductivityRatio < 0.4);

        if (lowFocusDays < 4) return null;

        var strength = lowFocusDays / 7.0;
        var evidence = JsonSerializer.SerializeToElement(new
        {
            lowFocusDays,
            avgFocus,
            lowFocusThreshold = avgFocus * afternoonFocusDropMultiplier,
        });

        return new PatternCandidate("afternoon_focus_drop", strength, evidence);
    }

    /// <summary>
    /// Detects users with frequent context switching (above threshold on 3+ days).
    /// </summary>
    public static PatternCandidate? TryDetectHighContextSwitching(
        List<DailyFocusScore> scores,
        int contextSwitchThreshold)
    {
        var daysWithHighSwitching = scores.Count(s => s.ContextSwitchesCount > contextSwitchThreshold);
        if (daysWithHighSwitching < 3) return null;

        var strength = daysWithHighSwitching / 7.0;
        var avgSwitches = scores.Average(s => s.ContextSwitchesCount);
        var evidence = JsonSerializer.SerializeToElement(new
        {
            daysWithHighSwitching,
            avgSwitches,
            threshold = contextSwitchThreshold,
        });

        return new PatternCandidate("high_context_switching", strength, evidence);
    }

    /// <summary>
    /// Detects users who can maintain deep focus sessions (longest focus > threshold on 2+ days).
    /// </summary>
    public static PatternCandidate? TryDetectDeepWorkCapable(
        List<DailyFocusScore> scores,
        int deepWorkSecondsThreshold)
    {
        var daysWithDeepWork = scores.Count(s => s.LongestFocusSeconds > deepWorkSecondsThreshold);
        if (daysWithDeepWork < 2) return null;

        var strength = daysWithDeepWork / 7.0;
        var avgLongest = (int)scores.Average(s => s.LongestFocusSeconds);
        var evidence = JsonSerializer.SerializeToElement(new
        {
            daysWithDeepWork,
            avgLongestMinutes = avgLongest / 60,
            thresholdMinutes = deepWorkSecondsThreshold / 60,
        });

        return new PatternCandidate("deep_work_capable", strength, evidence);
    }

    /// <summary>
    /// Detects users with high distraction ratios (distraction > threshold on 3+ days).
    /// </summary>
    public static PatternCandidate? TryDetectHighDistractionRisk(
        List<DailyFocusScore> scores,
        double distractionRatioThreshold)
    {
        var daysWithHighDistraction = scores.Count(s =>
        {
            var total = s.ProductiveSeconds + s.DistractionSeconds + s.NeutralSeconds;
            return total > 0 && s.DistractionSeconds > total * distractionRatioThreshold;
        });

        if (daysWithHighDistraction < 3) return null;

        var strength = daysWithHighDistraction / 7.0;
        var avgDistractionRatio = scores.Average(s =>
        {
            var total = s.ProductiveSeconds + s.DistractionSeconds + s.NeutralSeconds;
            return total > 0 ? (double)s.DistractionSeconds / total : 0;
        });
        var evidence = JsonSerializer.SerializeToElement(new
        {
            daysWithHighDistraction,
            avgDistractionRatio,
            threshold = distractionRatioThreshold,
        });

        return new PatternCandidate("high_distraction_risk", strength, evidence);
    }
}
