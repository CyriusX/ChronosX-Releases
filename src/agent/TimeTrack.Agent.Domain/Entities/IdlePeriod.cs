using TimeTrack.Agent.Domain.Common;
using TimeTrack.Agent.Domain.ValueObjects;

namespace TimeTrack.Agent.Domain.Entities;

/// <summary>
/// Representa um período de inatividade do usuário
/// </summary>
public sealed class IdlePeriod : EntityBase
{
    /// <summary>
    /// ID do usuário proprietário deste período
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// Intervalo de tempo do período de inatividade
    /// </summary>
    public TimeRange Period { get; private set; }

    /// <summary>
    /// Limiar em segundos que define o início da inatividade
    /// </summary>
    public int ThresholdSeconds { get; }

    /// <summary>
    /// Indica se o período foi detectado automaticamente pelo sistema
    /// </summary>
    public bool IsSystemDetected { get; }

    /// <summary>
    /// Estado da justificativa de inatividade neste dispositivo.
    /// </summary>
    public string JustificationState { get; private set; } = IdleJustificationStates.None;

    /// <summary>
    /// Código de motivo selecionado pelo usuário.
    /// </summary>
    public string? JustificationReasonCode { get; private set; }

    /// <summary>
    /// Nota opcional fornecida pelo usuário.
    /// </summary>
    public string? JustificationNote { get; private set; }

    /// <summary>
    /// Momento em que a justificativa foi enviada.
    /// </summary>
    public DateTime? JustificationSubmittedAtUtc { get; private set; }

    private IdlePeriod() { }

    public IdlePeriod(
        Guid id,
        Guid userId,
        TimeRange period,
        int thresholdSeconds,
        bool isSystemDetected = true)
        : base(id)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId is required", nameof(userId));

        if (thresholdSeconds <= 0)
            throw new DomainException("INVALID_THRESHOLD", "O limiar de inatividade deve ser maior que zero.");

        UserId = userId;
        Period = period ?? throw new ArgumentNullException(nameof(period));
        ThresholdSeconds = thresholdSeconds;
        IsSystemDetected = isSystemDetected;
    }

    /// <summary>
    /// Cria um novo período de inatividade com ID gerado automaticamente
    /// </summary>
    public static IdlePeriod Create(
        Guid userId,
        TimeRange period,
        int thresholdSeconds,
        bool isSystemDetected = true)
    {
        return new IdlePeriod(Guid.NewGuid(), userId, period, thresholdSeconds, isSystemDetected);
    }

    /// <summary>
    /// Duração do período de inatividade
    /// </summary>
    public TimeSpan Duration => Period.Duration;

    /// <summary>
    /// Verifica se o período de inatividade excede um determinado limiar
    /// </summary>
    public bool ExceedsThreshold(int seconds)
    {
        return Duration.TotalSeconds > seconds;
    }

    public void MarkJustificationPending()
    {
        if (JustificationState == IdleJustificationStates.Submitted)
            return;

        JustificationState = IdleJustificationStates.Pending;
    }

    public void DismissJustification()
    {
        if (JustificationState == IdleJustificationStates.Submitted)
            return;

        JustificationState = IdleJustificationStates.Dismissed;
    }

    public void SubmitJustification(string reasonCode, string? note, DateTime submittedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(reasonCode))
            throw new ArgumentException("Reason code is required", nameof(reasonCode));

        if (!string.IsNullOrWhiteSpace(note) && note.Length > 500)
            throw new ArgumentOutOfRangeException(nameof(note), "Justification note must be 500 characters or fewer");

        JustificationState = IdleJustificationStates.Submitted;
        JustificationReasonCode = reasonCode.Trim();
        JustificationNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        JustificationSubmittedAtUtc = submittedAtUtc;
    }

    public override string ToString()
        => $"Idle[{Id}] {Period} (threshold: {ThresholdSeconds}s)";
}

public static class IdleJustificationStates
{
    public const string None = "none";
    public const string Pending = "pending";
    public const string Submitted = "submitted";
    public const string Dismissed = "dismissed";
}
