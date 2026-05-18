namespace TimeTrack.Backend.Domain.Entities;

public sealed class SmartAlert
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? AboutUserId { get; private set; }
    public Guid OrgId { get; private set; }
    public string AlertType { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string Severity { get; private set; } = "info";
    public string? ActionType { get; private set; }
    public bool WasRead { get; private set; }
    public bool WasActed { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private SmartAlert() { }

    public static SmartAlert Create(
        Guid userId,
        Guid orgId,
        string alertType,
        string message,
        string severity = "info",
        string? actionType = null,
        Guid? aboutUserId = null)
    {
        return new SmartAlert
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrgId = orgId,
            AlertType = alertType,
            Message = message,
            Severity = severity,
            ActionType = actionType,
            AboutUserId = aboutUserId,
            WasRead = false,
            WasActed = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkRead() => WasRead = true;
    public void MarkActed() => WasActed = true;

    /// <summary>
    /// Creates an overwork alert when total active time exceeds the threshold.
    /// Returns null if the user is within normal limits.
    /// </summary>
    public static SmartAlert? TryCreateOverworkAlert(
        DailyFocusScore score,
        int overworkThresholdSeconds)
    {
        var totalActive = score.ProductiveSeconds + score.DistractionSeconds + score.NeutralSeconds;
        if (totalActive < overworkThresholdSeconds) return null;

        var message = $"Voce esteve ativo por {totalActive / 3600.0:F1}h sem registrar pausas. " +
                      $"Considere uma pausa para manter a produtividade.";

        return Create(score.UserId, score.OrgId, "overwork", message, "info");
    }

    /// <summary>
    /// Creates a focus drop alert when the user's focus score falls below their personal threshold.
    /// Returns null if focus score is within normal range.
    /// </summary>
    public static SmartAlert? TryCreateFocusDropAlert(
        DailyFocusScore score,
        double baselineAvgFocus,
        double focusDropMultiplier)
    {
        var threshold = baselineAvgFocus * focusDropMultiplier;
        if (score.FocusScore >= threshold) return null;

        var message = $"Seu focus score ({score.FocusScore}) esta abaixo do seu padrao " +
                      $"({baselineAvgFocus:F0}). Tente uma sessao de foco.";

        return Create(
            score.UserId, score.OrgId, "focus_drop", message, "warning",
            actionType: "start_pomodoro");
    }
}
