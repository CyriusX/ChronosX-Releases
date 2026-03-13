using TimeTrack.Agent.Domain.Common;
using TimeTrack.Agent.Domain.Enums;

namespace TimeTrack.Agent.Domain.Entities;

/// <summary>
/// Representa um ciclo de foco completo ou em andamento
///
/// SRP: Apenas encapsula dados de um ciclo de foco
/// OCP: Extensível para novos modos de foco
/// </summary>
public sealed class FocusCycle : EntityBase
{
    /// <summary>
    /// Modo de foco usado neste ciclo
    /// </summary>
    public FocusModeType Mode { get; private set; }

    /// <summary>
    /// Número do ciclo no dia (sequencial)
    /// </summary>
    public int CycleNumber { get; private set; }

    /// <summary>
    /// Momento em que o ciclo foi iniciado
    /// </summary>
    public DateTime StartedAt { get; private set; }

    /// <summary>
    /// Momento em que o ciclo foi finalizado (null se em andamento)
    /// </summary>
    public DateTime? EndedAt { get; private set; }

    /// <summary>
    /// Duração planejada em milissegundos
    /// </summary>
    public int PlannedMs { get; private set; }

    /// <summary>
    /// Duração real em milissegundos (null se em andamento)
    /// </summary>
    public int? ActualMs { get; private set; }

    /// <summary>
    /// Se o ciclo foi completado sem interrupção
    /// </summary>
    public bool Completed { get; private set; }

    /// <summary>
    /// Se a pausa foi tomada após o ciclo
    /// </summary>
    public bool BreakTaken { get; private set; }

    /// <summary>
    /// Se foi sincronizado com o backend
    /// </summary>
    public bool Synced { get; private set; }

    /// <summary>
    /// ID do usuário proprietário
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Data do ciclo (para agrupamento por dia)
    /// </summary>
    public DateTime Date { get; private set; }

    private FocusCycle() { }

    /// <summary>
    /// Cria um novo ciclo de foco
    /// </summary>
    public FocusCycle(
        Guid id,
        Guid userId,
        FocusModeType mode,
        int cycleNumber,
        int plannedMs) : base(id)
    {
        if (plannedMs <= 0)
            throw new ArgumentException("Planned duration must be positive", nameof(plannedMs));

        UserId = userId;
        Mode = mode;
        CycleNumber = cycleNumber;
        PlannedMs = plannedMs;
        StartedAt = DateTime.UtcNow;
        Date = DateTime.Today;
        Completed = false;
        BreakTaken = false;
        Synced = false;
    }

    /// <summary>
    /// Marca o ciclo como completado
    /// </summary>
    public void Complete()
    {
        if (Completed)
            return;

        EndedAt = DateTime.UtcNow;
        ActualMs = (int)(EndedAt.Value - StartedAt).TotalMilliseconds;
        Completed = true;
    }

    /// <summary>
    /// Marca que a pausa foi tomada
    /// </summary>
    public void MarkBreakTaken()
    {
        BreakTaken = true;
    }

    /// <summary>
    /// Marca como sincronizado
    /// </summary>
    public void MarkSynced()
    {
        Synced = true;
    }

    /// <summary>
    /// Cancela o ciclo (quando interrompido)
    /// </summary>
    public void Cancel()
    {
        if (Completed)
            return;

        EndedAt = DateTime.UtcNow;
        ActualMs = (int)(EndedAt.Value - StartedAt).TotalMilliseconds;
        Completed = false;
    }
}
