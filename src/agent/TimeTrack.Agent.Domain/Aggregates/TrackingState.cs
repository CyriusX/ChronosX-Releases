using System.Text.Json;
using System.Text.Json.Serialization;
using TimeTrack.Agent.Domain.Common;
using TimeTrack.Agent.Domain.Enums;
using TimeTrack.Agent.Domain.Events;

namespace TimeTrack.Agent.Domain.Aggregates;

/// <summary>
/// Agregado que representa o estado do tracking com máquina de estados
/// </summary>
public sealed class TrackingState : EntityBase
{
    private readonly List<IDomainEvent> _events = new();

    /// <summary>
    /// ID do usuário proprietário deste estado
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Status atual do tracking
    /// </summary>
    public TrackingStatus Status { get; private set; }

    /// <summary>
    /// Motivo da pausa (quando aplicável)
    /// </summary>
    public string? Reason { get; private set; }

    /// <summary>
    /// Momento em que foi pausado
    /// </summary>
    public DateTime? PausedAt { get; private set; }

    /// <summary>
    /// Momento em que foi retomado
    /// </summary>
    public DateTime? ResumedAt { get; private set; }

    /// <summary>
    /// Momento da última atualização
    /// </summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>
    /// Identificador de quem fez a última alteração
    /// </summary>
    public string? LastModifiedBy { get; private set; }

    /// <summary>
    /// Eventos de domínio gerados
    /// </summary>
    public IReadOnlyList<IDomainEvent> Events => _events.AsReadOnly();

    /// <summary>
    /// Indica se o tracking está ativo
    /// </summary>
    public bool IsActive => Status == TrackingStatus.Active;

    /// <summary>
    /// Indica se o tracking está pausado
    /// </summary>
    public bool IsPaused => Status == TrackingStatus.PausedByUser ||
                            Status == TrackingStatus.PausedByPolicy;

    /// <summary>
    /// Indica se o tracking está desativado
    /// </summary>
    public bool IsDisabled => Status == TrackingStatus.Disabled;

    private TrackingState() { }

    public TrackingState(Guid id, Guid userId) : base(id)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId is required", nameof(userId));

        UserId = userId;
        Status = TrackingStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Cria um novo estado de tracking ativo para um usuário
    /// </summary>
    public static TrackingState CreateActive(Guid userId)
    {
        return new TrackingState(Guid.NewGuid(), userId);
    }

    /// <summary>
    /// Pausa o tracking
    /// </summary>
    /// <param name="reason">Motivo da pausa</param>
    /// <param name="pausedBy">Quem pausou (user_id ou "system")</param>
    /// <param name="isPolicy">Se é pausa por política</param>
    public void Pause(string reason, string pausedBy, bool isPolicy = false)
    {
        if (Status != TrackingStatus.Active)
        {
            throw new DomainException(
                "INVALID_TRANSITION",
                $"Não é possível pausar a partir do estado '{Status}'. Apenas 'Active' pode ser pausado.");
        }

        var previousStatus = Status;
        Status = isPolicy ? TrackingStatus.PausedByPolicy : TrackingStatus.PausedByUser;
        Reason = reason ?? string.Empty;
        PausedAt = DateTime.UtcNow;
        ResumedAt = null;
        LastModifiedBy = pausedBy;
        UpdatedAt = DateTime.UtcNow;

        _events.Add(new TrackingPaused(Reason, pausedBy, PausedAt));
    }

    /// <summary>
    /// Retoma o tracking
    /// </summary>
    /// <param name="resumedBy">Quem retomou (user_id ou "system")</param>
    public void Resume(string resumedBy)
    {
        if (!IsPaused)
        {
            throw new DomainException(
                "INVALID_TRANSITION",
                $"Não é possível retomar a partir do estado '{Status}'. Apenas estados pausados podem ser retomados.");
        }

        Status = TrackingStatus.Active;
        Reason = null;
        ResumedAt = DateTime.UtcNow;
        LastModifiedBy = resumedBy;
        UpdatedAt = DateTime.UtcNow;

        _events.Add(new TrackingResumed(resumedBy, ResumedAt));
    }

    /// <summary>
    /// Desativa o tracking
    /// </summary>
    public void Disable(string? reason = null)
    {
        if (Status == TrackingStatus.Disabled)
        {
            throw new DomainException(
                "INVALID_TRANSITION",
                "O tracking já está desativado.");
        }

        Status = TrackingStatus.Disabled;
        Reason = reason ?? "Disabled by administrator";
        LastModifiedBy = "admin";
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Reativa o tracking
    /// </summary>
    public void Enable()
    {
        if (Status != TrackingStatus.Disabled)
        {
            throw new DomainException(
                "INVALID_TRANSITION",
                $"Não é possível ativar a partir do estado '{Status}'. Apenas 'Disabled' pode ser ativado.");
        }

        Status = TrackingStatus.Active;
        Reason = null;
        PausedAt = null;
        ResumedAt = null;
        LastModifiedBy = "admin";
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Limpa os eventos de domínio
    /// </summary>
    public void ClearEvents()
    {
        _events.Clear();
    }

    #region Serialization

    /// <summary>
    /// Dados para serialização
    /// </summary>
    public TrackingStateDto ToDto()
    {
        return new TrackingStateDto
        {
            Id = Id,
            UserId = UserId,
            Status = Status,
            Reason = Reason,
            PausedAt = PausedAt,
            ResumedAt = ResumedAt,
            UpdatedAt = UpdatedAt,
            LastModifiedBy = LastModifiedBy
        };
    }

    /// <summary>
    /// Restaura o estado a partir dos dados serializados
    /// </summary>
    public static TrackingState FromDto(TrackingStateDto dto)
    {
        var state = new TrackingState(dto.Id, dto.UserId)
        {
            Status = dto.Status,
            Reason = dto.Reason,
            PausedAt = dto.PausedAt,
            ResumedAt = dto.ResumedAt,
            UpdatedAt = dto.UpdatedAt,
            LastModifiedBy = dto.LastModifiedBy
        };
        return state;
    }

    #endregion
}

/// <summary>
/// DTO para serialização do TrackingState
/// </summary>
public sealed class TrackingStateDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public TrackingStatus Status { get; set; }
    public string? Reason { get; set; }
    public DateTime? PausedAt { get; set; }
    public DateTime? ResumedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? LastModifiedBy { get; set; }
}
