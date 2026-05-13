namespace TimeTrack.Backend.Domain.Entities;

/// <summary>
/// Configuracao de envio de relatorio semanal personalizado por e-mail.
/// Cada usuario pode ter no maximo um schedule ativo.
/// </summary>
public sealed class WeeklyReportSchedule
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid OrgId { get; private set; }

    /// <summary>
    /// Dia da semana para envio (0=Sunday .. 6=Saturday)
    /// </summary>
    public int DayOfWeek { get; private set; }

    /// <summary>
    /// Horario de envio (sem fuso - converte-se via timezone da org)
    /// </summary>
    public TimeOnly TimeOfDay { get; private set; }

    public bool IsEnabled { get; private set; } = true;

    /// <summary>
    /// Preferencias do relatorio serializadas como JSON.
    /// Veja <see cref="ValueObjects.ReportPreferences"/>.
    /// </summary>
    public string PreferencesJson { get; private set; } = "{}";

    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private WeeklyReportSchedule() { }

    public static WeeklyReportSchedule Create(
        Guid userId,
        Guid orgId,
        int dayOfWeek,
        TimeOnly timeOfDay,
        string? preferencesJson = null)
    {
        ValidateDayOfWeek(dayOfWeek);

        return new WeeklyReportSchedule
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrgId = orgId,
            DayOfWeek = dayOfWeek,
            TimeOfDay = timeOfDay,
            IsEnabled = true,
            PreferencesJson = preferencesJson ?? "{}",
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(int dayOfWeek, TimeOnly timeOfDay, string? preferencesJson = null)
    {
        ValidateDayOfWeek(dayOfWeek);

        DayOfWeek = dayOfWeek;
        TimeOfDay = timeOfDay;

        if (preferencesJson is not null)
            PreferencesJson = preferencesJson;

        UpdatedAt = DateTime.UtcNow;
    }

    public void Enable()
    {
        IsEnabled = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Disable()
    {
        IsEnabled = false;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Returns the System.DayOfWeek equivalent for schedule matching.
    /// </summary>
    public DayOfWeek GetSystemDayOfWeek() => (DayOfWeek)DayOfWeek;

    private static void ValidateDayOfWeek(int dayOfWeek)
    {
        if (dayOfWeek < 0 || dayOfWeek > 6)
            throw new ArgumentOutOfRangeException(nameof(dayOfWeek), "Day of week must be between 0 (Sunday) and 6 (Saturday)");
    }
}
