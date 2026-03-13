using FluentValidation;
using System.Collections.Generic;
using System.Linq;

namespace TimeTrack.Backend.Application.Policies.Validators;

/// <summary>
/// Validator for UpdateOrgPolicyRequest
/// </summary>
public sealed class UpdateOrgPolicyRequestValidator : AbstractValidator<DTOs.UpdateOrgPolicyRequest>
{
    private static readonly HashSet<string> ValidDays = new(StringComparer.OrdinalIgnoreCase)
    {
        "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday"
    };

    private static readonly HashSet<int> ValidRetentionDays = new() { 30, 60, 90, 180, 365 };

    public UpdateOrgPolicyRequestValidator()
    {
        RuleFor(x => x.WorkHours)
            .SetValidator(new WorkHoursDtoValidator()!)
            .When(x => x.WorkHours is not null);

        RuleFor(x => x.AppExclusions)
            .Must(BeValidAppExclusions)
            .WithMessage("App exclusions must be a list of non-empty strings")
            .When(x => x.AppExclusions is not null);

        RuleFor(x => x.IdleThresholdSeconds)
            .InclusiveBetween(60, 3600)
            .WithMessage("Idle threshold must be between 60 and 3600 seconds")
            .When(x => x.IdleThresholdSeconds.HasValue);

        RuleFor(x => x.RetentionDays)
            .Must(BeValidRetentionDays)
            .WithMessage("Retention days must be one of: 30, 60, 90, 180, 365")
            .When(x => x.RetentionDays.HasValue);
    }

    private static bool BeValidAppExclusions(List<string>? exclusions)
    {
        if (exclusions == null) return true;
        return exclusions.All(e => !string.IsNullOrWhiteSpace(e));
    }

    private static bool BeValidRetentionDays(int? days)
    {
        if (!days.HasValue) return true;
        return ValidRetentionDays.Contains(days.Value);
    }
}

/// <summary>
/// Validator for WorkHoursDto
/// </summary>
public sealed class WorkHoursDtoValidator : AbstractValidator<DTOs.WorkHoursDto>
{
    private static readonly HashSet<string> ValidDays = new(StringComparer.OrdinalIgnoreCase)
    {
        "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday"
    };

    // Common IANA timezone IDs
    private static readonly HashSet<string> CommonTimezones = new(StringComparer.OrdinalIgnoreCase)
    {
        // Americas
        "America/Sao_Paulo", "America/New_York", "America/Los_Angeles", "America/Chicago",
        "America/Denver", "America/Toronto", "America/Vancouver", "America/Mexico_City",
        "America/Buenos_Aires", "America/Lima", "America/Bogota", "America/Santiago",
        // Europe
        "Europe/London", "Europe/Paris", "Europe/Berlin", "Europe/Madrid", "Europe/Rome",
        "Europe/Amsterdam", "Europe/Brussels", "Europe/Vienna", "Europe/Warsaw",
        // Asia
        "Asia/Tokyo", "Asia/Shanghai", "Asia/Hong_Kong", "Asia/Singapore", "Asia/Seoul",
        "Asia/Dubai", "Asia/Kolkata", "Asia/Jakarta", "Asia/Manila",
        // Oceania
        "Australia/Sydney", "Australia/Melbourne", "Australia/Perth", "Pacific/Auckland",
        // UTC
        "UTC", "Etc/UTC"
    };

    public WorkHoursDtoValidator()
    {
        RuleFor(x => x.Timezone)
            .Must(BeValidTimezone)
            .WithMessage("Timezone must be a valid IANA timezone identifier (e.g., 'America/Sao_Paulo')");

        RuleFor(x => x.Days)
            .NotEmpty()
            .WithMessage("At least one work day must be specified")
            .Must(BeValidDays)
            .WithMessage("Days must be valid day names: monday, tuesday, wednesday, thursday, friday, saturday, sunday");

        RuleFor(x => x.StartTime)
            .Must(BeValidTimeFormat)
            .WithMessage("Start time must be in HH:mm format (24-hour)");

        RuleFor(x => x.EndTime)
            .Must(BeValidTimeFormat)
            .WithMessage("End time must be in HH:mm format (24-hour)");

        RuleFor(x => x)
            .Must(BeValidTimeRange)
            .WithMessage("End time must be after start time");
    }

    private static bool BeValidTimezone(string? timezone)
    {
        if (string.IsNullOrWhiteSpace(timezone)) return false;

        // First check common timezones
        if (CommonTimezones.Contains(timezone)) return true;

        // Then try to validate using TimeZoneInfo
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timezone);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool BeValidDays(List<string>? days)
    {
        if (days == null || days.Count == 0) return false;
        return days.All(d => ValidDays.Contains(d));
    }

    private static bool BeValidTimeFormat(string? time)
    {
        if (string.IsNullOrWhiteSpace(time)) return false;

        // Must be in HH:mm format
        if (time.Length != 5) return false;
        if (time[2] != ':') return false;

        if (!int.TryParse(time.AsSpan(0, 2), out var hours)) return false;
        if (!int.TryParse(time.AsSpan(3, 2), out var minutes)) return false;

        return hours >= 0 && hours <= 23 && minutes >= 0 && minutes <= 59;
    }

    private static bool BeValidTimeRange(DTOs.WorkHoursDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.StartTime) || string.IsNullOrWhiteSpace(dto.EndTime))
            return true; // Let other validators handle null/empty

        return string.Compare(dto.StartTime, dto.EndTime, StringComparison.Ordinal) < 0;
    }
}
