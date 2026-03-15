namespace TimeTrack.Backend.Domain.ValueObjects;

/// <summary>
/// Dados de entrada para cálculo do Focus Score
///
/// SRP: Apenas transporta dados para o cálculo
/// </summary>
public sealed record FocusScoreInput
{
    /// <summary>
    /// Tempo total rastreado no dia (milissegundos)
    /// </summary>
    public long TotalTrackedMs { get; init; }

    /// <summary>
    /// Tempo em aplicações produtivas (milissegundos)
    /// </summary>
    public long FocusTimeMs { get; init; }

    /// <summary>
    /// Tempo em aplicações de distração (milissegundos)
    /// </summary>
    public long DistractionMs { get; init; }

    /// <summary>
    /// Número de trocas para apps de distração
    /// </summary>
    public int DistractionCount { get; init; }

    /// <summary>
    /// Número de pausas manuais
    /// </summary>
    public int PauseCount { get; init; }

    /// <summary>
    /// Número de períodos idle detectados
    /// </summary>
    public int IdleCount { get; init; }

    /// <summary>
    /// Número de blocos de foco contínuo (> 25min sem interrupção)
    /// </summary>
    public int LongFocusBlockCount { get; init; }

    /// <summary>
    /// Cria uma instância vazia com valores padrão
    /// </summary>
    public static FocusScoreInput Empty => new();

    /// <summary>
    /// Cria uma instância com os valores especificados
    /// </summary>
    public static FocusScoreInput Create(
        long totalTrackedMs,
        long focusTimeMs,
        long distractionMs,
        int distractionCount,
        int pauseCount,
        int idleCount,
        int longFocusBlockCount)
    {
        return new FocusScoreInput
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
