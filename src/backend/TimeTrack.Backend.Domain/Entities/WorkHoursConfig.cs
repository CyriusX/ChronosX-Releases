namespace TimeTrack.Backend.Domain.Entities;

/// <summary>
/// Configuration for work hours policy
/// SRP: Apenas encapsula configuração de horário de trabalho
/// </summary>
public sealed class WorkHoursConfig
{
    public string Timezone { get; set; } = "America/Sao_Paulo";
    public List<string> Days { get; set; } = new();
    public string StartTime { get; set; } = "08:00";
    public string EndTime { get; set; } = "18:00";

    /// <summary>
    /// Checks if a given UTC datetime falls within the work hours window
    /// </summary>
    public bool IsWithinWindow(DateTimeOffset utcTime)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(Timezone);
            var local = TimeZoneInfo.ConvertTimeFromUtc(utcTime.UtcDateTime, tz);

            var dayName = local.DayOfWeek.ToString().ToLowerInvariant();
            if (!Days.Contains(dayName))
                return false;

            var localTime = TimeOnly.Parse($"{local.Hour:D2}:{local.Minute:D2}");
            var startTime = TimeOnly.Parse(StartTime);
            var endTime = TimeOnly.Parse(EndTime);

            return localTime >= startTime && localTime <= endTime;
        }
        catch
        {
            // If timezone is invalid, default to true (don't block)
            return true;
        }
    }
}
