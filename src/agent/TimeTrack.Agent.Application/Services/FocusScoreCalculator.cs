namespace TimeTrack.Agent.Application.Services;

/// <summary>
/// Calculador de Focus Score local do Agent
///
/// SRP: Apenas calcula o score baseado nas métricas de entrada
/// </summary>
public static class FocusScoreCalculator
{
    // Constants (Algoritmo configurável)
    private const double DistractionPenaltyPerCount = 2.5;
    private const double MaxDistractionPenalty = 25.0;
    private const double PausePenaltyPerCount = 1.5;
    private const double MaxPausePenalty = 15.0;
    private const double LongFocusBlockBonus = 3.0;
    private const double MaxLongFocusBlockBonus = 10.0;

    /// <summary>
    /// Calcula o Focus Score (0-100)
    /// </summary>
    public static short Calculate(FocusScoreMetrics metrics)
    {
        // 1. Base: % de tempo em apps produtivos vs total
        var productivityRatio = metrics.TotalTrackedMs > 0
            ? (double)metrics.FocusTimeMs / metrics.TotalTrackedMs
            : 0;

        var baseScore = productivityRatio * 100;

        // 2. Penalidades
        var distractionPenalty = Math.Min(
            metrics.DistractionCount * DistractionPenaltyPerCount,
            MaxDistractionPenalty);

        var pausePenalty = Math.Min(
            metrics.PauseCount * PausePenaltyPerCount,
            MaxPausePenalty);

        // 3. Bônus por blocos de foco contínuo
        var focusBonus = Math.Min(
            metrics.LongFocusBlockCount * LongFocusBlockBonus,
            MaxLongFocusBlockBonus);

        // 4. Score final
        var rawScore = baseScore - distractionPenalty - pausePenalty + focusBonus;

        // 5. Clamp to valid range
        return (short)Math.Clamp(Math.Round(rawScore), 0, 100);
    }
}

/// <summary>
/// Métricas para cálculo do Focus Score
/// </summary>
public sealed record FocusScoreMetrics
{
    public long TotalTrackedMs { get; init; }
    public long FocusTimeMs { get; init; }
    public long DistractionMs { get; init; }
    public int DistractionCount { get; init; }
    public int PauseCount { get; init; }
    public int IdleCount { get; init; }
    public int LongFocusBlockCount { get; init; }

    public static FocusScoreMetrics Empty => new();

    public static FocusScoreMetrics Create(
        long totalTrackedMs,
        long focusTimeMs,
        long distractionMs,
        int distractionCount,
        int pauseCount,
        int idleCount,
        int longFocusBlockCount)
    {
        return new FocusScoreMetrics
        {
            TotalTrackedMs = totalTrackedMs,
            FocusTimeMs = focusTimeMs,
            DistractionMs = distractionMs,
            DistractionCount = distractionCount,
            PauseCount = pauseCount,
            IdleCount = idleCount,
            LongFocusBlockCount = longFocusBlockCount
        };
    }
}
