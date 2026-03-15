using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.FocusScore;

/// <summary>
/// Serviço de domínio para cálculo do Focus Score
///
/// Algoritmo baseado em:
/// 1. Base: % de tempo em apps produtivos vs total
/// 2. Penalidades: distrações e pausas
/// 3. Bônus: blocos de foco contínuo longos
///
/// SRP: Apenas calcula o score - não classifica apps nem acessa banco
/// OCP: Extensível via parâmetros sem modificar o algoritmo
/// DIP: Sem dependências externas (pure function)
/// </summary>
public static class FocusScoreCalculator
{
    // ============================================================================
    // Constants (Algoritmo configurável)
    // ============================================================================

    /// <summary>
    /// Penalidade por cada troca para app de distração
    /// </summary>
    private const double DistractionPenaltyPerCount = 2.5;

    /// <summary>
    /// Penalidade máxima acumulada por distrações
    /// </summary>
    private const double MaxDistractionPenalty = 25.0;

    /// <summary>
    /// Penalidade por cada pausa manual
    /// </summary>
    private const double PausePenaltyPerCount = 1.5;

    /// <summary>
    /// Penalidade máxima acumulada por pausas
    /// </summary>
    private const double MaxPausePenalty = 15.0;

    /// <summary>
    /// Bônus por cada bloco de foco longo (>25min sem interrupção)
    /// </summary>
    private const double LongFocusBlockBonus = 3.0;

    /// <summary>
    /// Bônus máximo acumulado por blocos de foco
    /// </summary>
    private const double MaxLongFocusBlockBonus = 10.0;

    // ============================================================================
    // Public API
    // ============================================================================

    /// <summary>
    /// Calcula o Focus Score (0-100) baseado nas métricas de entrada
    /// </summary>
    /// <param name="input">Dados de entrada para o cálculo</param>
    /// <returns>Score entre 0 e 100</returns>
    public static short Calculate(FocusScoreInput input)
    {
        // 1. Base: % de tempo em apps produtivos vs total
        var productivityRatio = CalculateProductivityRatio(input);
        var baseScore = productivityRatio * 100;

        // 2. Penalidades
        var distractionPenalty = CalculateDistractionPenalty(input.DistractionCount);
        var pausePenalty = CalculatePausePenalty(input.PauseCount);

        // 3. Bônus por blocos de foco contínuo
        var focusBonus = CalculateFocusBonus(input.LongFocusBlockCount);

        // 4. Score final
        var rawScore = baseScore - distractionPenalty - pausePenalty + focusBonus;

        // 5. Clamp to valid range
        return (short)Math.Clamp(Math.Round(rawScore), 0, 100);
    }

    /// <summary>
    /// Calcula o score com breakdown detalhado (útil para debugging/análise)
    /// </summary>
    public static FocusScoreBreakdown CalculateWithBreakdown(FocusScoreInput input)
    {
        var productivityRatio = CalculateProductivityRatio(input);
        var baseScore = productivityRatio * 100;
        var distractionPenalty = CalculateDistractionPenalty(input.DistractionCount);
        var pausePenalty = CalculatePausePenalty(input.PauseCount);
        var focusBonus = CalculateFocusBonus(input.LongFocusBlockCount);
        var rawScore = baseScore - distractionPenalty - pausePenalty + focusBonus;
        var finalScore = (short)Math.Clamp(Math.Round(rawScore), 0, 100);

        return new FocusScoreBreakdown
        {
            FinalScore = finalScore,
            ProductivityRatio = productivityRatio,
            BaseScore = baseScore,
            DistractionPenalty = distractionPenalty,
            PausePenalty = pausePenalty,
            FocusBonus = focusBonus,
            RawScore = rawScore
        };
    }

    // ============================================================================
    // Private Calculation Methods (SRP: cada método faz uma coisa)
    // ============================================================================

    private static double CalculateProductivityRatio(FocusScoreInput input)
    {
        if (input.TotalTrackedMs <= 0)
            return 0;

        return (double)input.FocusTimeMs / input.TotalTrackedMs;
    }

    private static double CalculateDistractionPenalty(int distractionCount)
    {
        return Math.Min(distractionCount * DistractionPenaltyPerCount, MaxDistractionPenalty);
    }

    private static double CalculatePausePenalty(int pauseCount)
    {
        return Math.Min(pauseCount * PausePenaltyPerCount, MaxPausePenalty);
    }

    private static double CalculateFocusBonus(int longFocusBlockCount)
    {
        return Math.Min(longFocusBlockCount * LongFocusBlockBonus, MaxLongFocusBlockBonus);
    }
}

/// <summary>
/// Breakdown detalhado do cálculo do Focus Score
/// Útil para debugging e análise
/// </summary>
public sealed record FocusScoreBreakdown
{
    public short FinalScore { get; init; }
    public double ProductivityRatio { get; init; }
    public double BaseScore { get; init; }
    public double DistractionPenalty { get; init; }
    public double PausePenalty { get; init; }
    public double FocusBonus { get; init; }
    public double RawScore { get; init; }

    /// <summary>
    /// Percentual de tempo produtivo formatado
    /// </summary>
    public double ProductivePercentage => ProductivityRatio * 100;

    /// <summary>
    /// Score arredondado antes do clamp
    /// </summary>
    public double RawScoreRounded => Math.Round(RawScore);
}
