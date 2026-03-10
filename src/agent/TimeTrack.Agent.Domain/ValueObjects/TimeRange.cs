using TimeTrack.Agent.Domain.Common;

namespace TimeTrack.Agent.Domain.ValueObjects;

/// <summary>
/// Representa um intervalo de tempo com início e fim
/// </summary>
public sealed record TimeRange
{
    /// <summary>
    /// Momento de início em UTC
    /// </summary>
    public DateTime StartUtc { get; }

    /// <summary>
    /// Momento de término em UTC
    /// </summary>
    public DateTime EndUtc { get; }

    /// <summary>
    /// Duração do intervalo
    /// </summary>
    public TimeSpan Duration => EndUtc - StartUtc;

    private TimeRange() { }

    public TimeRange(DateTime startUtc, DateTime endUtc)
    {
        if (endUtc <= startUtc)
            throw DomainException.InvalidTimeRange();

        StartUtc = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);
        EndUtc = DateTime.SpecifyKind(endUtc, DateTimeKind.Utc);
    }

    /// <summary>
    /// Cria um TimeRange a partir de uma duração (início = agora)
    /// </summary>
    public static TimeRange FromDuration(TimeSpan duration)
    {
        var now = DateTime.UtcNow;
        return new TimeRange(now, now.Add(duration));
    }

    /// <summary>
    /// Cria um TimeRange a partir de um momento de início e duração
    /// </summary>
    public static TimeRange FromStartAndDuration(DateTime startUtc, TimeSpan duration)
    {
        return new TimeRange(startUtc, startUtc.Add(duration));
    }

    /// <summary>
    /// Verifica se este intervalo sobrepõe outro
    /// </summary>
    public bool Overlaps(TimeRange other)
    {
        return StartUtc < other.EndUtc && EndUtc > other.StartUtc;
    }

    /// <summary>
    /// Verifica se um momento está dentro do intervalo
    /// </summary>
    public bool Contains(DateTime moment)
    {
        return moment >= StartUtc && moment <= EndUtc;
    }

    public override string ToString()
        => $"[{StartUtc:O} - {EndUtc:O}] ({Duration.TotalSeconds:F0}s)";
}
