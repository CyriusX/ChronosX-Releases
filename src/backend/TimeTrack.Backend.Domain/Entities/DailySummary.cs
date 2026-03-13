using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Domain.Entities;

/// <summary>
/// Representa um resumo diário pré-calculado por usuário
/// Usado para acelerar consultas de relatórios sem processar sessões raw em tempo real
/// </summary>
public sealed class DailySummary
{
    public Guid Id { get; private set; }
    public Guid OrgId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime Date { get; private set; }
    public int TotalActiveSeconds { get; private set; }
    public int TotalIdleSeconds { get; private set; }
    public int SessionCount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    // Navigation properties
    public User? User { get; private set; }

    private DailySummary() { }

    /// <summary>
    /// Cria um novo resumo diário
    /// </summary>
    public static DailySummary Create(
        Guid id,
        Guid orgId,
        Guid userId,
        DateTime date,
        int totalActiveSeconds,
        int totalIdleSeconds,
        int sessionCount)
    {
        if (totalActiveSeconds < 0)
            throw new ArgumentException("Total active seconds cannot be negative", nameof(totalActiveSeconds));

        if (totalIdleSeconds < 0)
            throw new ArgumentException("Total idle seconds cannot be negative", nameof(totalIdleSeconds));

        if (sessionCount < 0)
            throw new ArgumentException("Session count cannot be negative", nameof(sessionCount));

        return new DailySummary
        {
            Id = id,
            OrgId = orgId,
            UserId = userId,
            Date = date.Date, // Ensure date-only, no time component
            TotalActiveSeconds = totalActiveSeconds,
            TotalIdleSeconds = totalIdleSeconds,
            SessionCount = sessionCount,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Atualiza um resumo diário existente (para reprocessamento idempotente)
    /// </summary>
    public void Update(
        int totalActiveSeconds,
        int totalIdleSeconds,
        int sessionCount)
    {
        if (totalActiveSeconds < 0)
            throw new ArgumentException("Total active seconds cannot be negative", nameof(totalActiveSeconds));

        if (totalIdleSeconds < 0)
            throw new ArgumentException("Total idle seconds cannot be negative", nameof(totalIdleSeconds));

        if (sessionCount < 0)
            throw new ArgumentException("Session count cannot be negative", nameof(sessionCount));

        TotalActiveSeconds = totalActiveSeconds;
        TotalIdleSeconds = totalIdleSeconds;
        SessionCount = sessionCount;
        UpdatedAt = DateTime.UtcNow;
    }
}
