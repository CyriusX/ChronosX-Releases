using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Domain.Entities;

/// <summary>
/// Representa o score de foco diário calculado para um usuário
///
/// Diferente de FocusSession (sessões Pomodoro/Ultradian), este é um
/// aggregate diário calculado a partir das activity_sessions.
///
/// SRP: Gerencia apenas o estado e invariantes do score diário
/// </summary>
public sealed class DailyFocusScore
{
    // ============================================================================
    // Properties
    // ============================================================================

    public Guid Id { get; private set; }
    public Guid OrgId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? DeviceId { get; private set; }
    public DateOnly Date { get; private set; }

    // Métricas de tempo (em milissegundos)
    public long TotalTrackedMs { get; private set; }
    public long FocusTimeMs { get; private set; }
    public long DistractionMs { get; private set; }

    // Métricas de comportamento
    public int DistractionCount { get; private set; }
    public int PauseCount { get; private set; }
    public int IdleCount { get; private set; }
    public int LongFocusBlockCount { get; private set; }

    // Score calculado (0-100)
    public short FocusScore { get; private set; }

    // Timestamps
    public DateTimeOffset CalculatedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    // Navigation properties
    public User? User { get; private set; }
    public Device? Device { get; private set; }

    // ============================================================================
    // Constructor (EF Core)
    // ============================================================================

    private DailyFocusScore() { }

    // ============================================================================
    // Factory Method
    // ============================================================================

    /// <summary>
    /// Cria um novo score diário de foco
    /// </summary>
    public static DailyFocusScore Create(
        Guid id,
        Guid orgId,
        Guid userId,
        DateOnly date,
        long totalTrackedMs,
        long focusTimeMs,
        long distractionMs,
        int distractionCount,
        int pauseCount,
        int idleCount,
        int longFocusBlockCount,
        short focusScore,
        Guid? deviceId = null)
    {
        ValidateMetrics(totalTrackedMs, focusTimeMs, distractionMs);
        ValidateScore(focusScore);

        return new DailyFocusScore
        {
            Id = id,
            OrgId = orgId,
            UserId = userId,
            DeviceId = deviceId,
            Date = date,
            TotalTrackedMs = totalTrackedMs,
            FocusTimeMs = focusTimeMs,
            DistractionMs = distractionMs,
            DistractionCount = Math.Max(0, distractionCount),
            PauseCount = Math.Max(0, pauseCount),
            IdleCount = Math.Max(0, idleCount),
            LongFocusBlockCount = Math.Max(0, longFocusBlockCount),
            FocusScore = focusScore,
            CalculatedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    // ============================================================================
    // Behavior Methods
    // ============================================================================

    /// <summary>
    /// Atualiza o score com novos dados calculados (reprocessamento idempotente)
    /// </summary>
    public void Recalculate(
        long totalTrackedMs,
        long focusTimeMs,
        long distractionMs,
        int distractionCount,
        int pauseCount,
        int idleCount,
        int longFocusBlockCount,
        short focusScore)
    {
        ValidateMetrics(totalTrackedMs, focusTimeMs, distractionMs);
        ValidateScore(focusScore);

        TotalTrackedMs = totalTrackedMs;
        FocusTimeMs = focusTimeMs;
        DistractionMs = distractionMs;
        DistractionCount = Math.Max(0, distractionCount);
        PauseCount = Math.Max(0, pauseCount);
        IdleCount = Math.Max(0, idleCount);
        LongFocusBlockCount = Math.Max(0, longFocusBlockCount);
        FocusScore = focusScore;
        CalculatedAt = DateTimeOffset.UtcNow;
    }

    // ============================================================================
    // Computed Properties
    // ============================================================================

    /// <summary>
    /// Percentual de tempo em apps produtivos
    /// </summary>
    public double ProductivityPercentage => TotalTrackedMs > 0
        ? (double)FocusTimeMs / TotalTrackedMs * 100
        : 0;

    /// <summary>
    /// Percentual de tempo em apps de distração
    /// </summary>
    public double DistractionPercentage => TotalTrackedMs > 0
        ? (double)DistractionMs / TotalTrackedMs * 100
        : 0;

    /// <summary>
    /// Classificação qualitativa do score
    /// </summary>
    public FocusScoreLevel Level => FocusScore switch
    {
        >= 80 => FocusScoreLevel.Excellent,
        >= 60 => FocusScoreLevel.Good,
        >= 40 => FocusScoreLevel.Moderate,
        >= 20 => FocusScoreLevel.Low,
        _ => FocusScoreLevel.Poor
    };

    // ============================================================================
    // Validation
    // ============================================================================

    private static void ValidateMetrics(long totalTrackedMs, long focusTimeMs, long distractionMs)
    {
        if (totalTrackedMs < 0)
            throw new ArgumentException("Total tracked time cannot be negative", nameof(totalTrackedMs));

        if (focusTimeMs < 0)
            throw new ArgumentException("Focus time cannot be negative", nameof(focusTimeMs));

        if (distractionMs < 0)
            throw new ArgumentException("Distraction time cannot be negative", nameof(distractionMs));

        if (focusTimeMs + distractionMs > totalTrackedMs)
            throw new ArgumentException("Focus + Distraction time cannot exceed total tracked time");
    }

    private static void ValidateScore(short focusScore)
    {
        if (focusScore < 0 || focusScore > 100)
            throw new ArgumentOutOfRangeException(nameof(focusScore), "Focus score must be between 0 and 100");
    }
}

/// <summary>
/// Níveis qualitativos do Focus Score
/// </summary>
public enum FocusScoreLevel
{
    Poor = 1,       // 0-19
    Low = 2,        // 20-39
    Moderate = 3,   // 40-59
    Good = 4,       // 60-79
    Excellent = 5   // 80-100
}
