using TimeTrack.Agent.Domain.Enums;
using TimeTrack.Agent.Domain.ValueObjects;

namespace TimeTrack.Agent.Contracts.Services;

/// <summary>
/// Snapshot do estado atual do motor de modo de foco
///
/// SRP: Apenas representa o estado momentâneo do engine
/// </summary>
public sealed class FocusModeSnapshot
{
    /// <summary>
    /// Estado atual do engine
    /// </summary>
    public FocusModeState State { get; init; }

    /// <summary>
    /// Tipo de modo de foco ativo
    /// </summary>
    public FocusModeType Mode { get; init; }

    /// <summary>
    /// Tempo restante em milissegundos no ciclo atual
    /// </summary>
    public int RemainingMs { get; init; }

    /// <summary>
    /// Número do ciclo atual no dia
    /// </summary>
    public int CycleNumber { get; init; }

    /// <summary>
    /// Total de ciclos completados hoje
    /// </summary>
    public int TotalCyclesToday { get; init; }

    /// <summary>
    /// Tipo da próxima pausa (apenas Pomodoro)
    /// </summary>
    public BreakType NextBreakType { get; init; }

    /// <summary>
    /// Quando o ciclo atual foi iniciado
    /// </summary>
    public DateTime? CycleStartedAt { get; init; }

    /// <summary>
    /// Duração planejada do ciclo atual em ms
    /// </summary>
    public int PlannedDurationMs { get; init; }

    /// <summary>
    /// Se o usuário pode iniciar/parar manualmente
    /// </summary>
    public bool AllowUserOverride { get; init; }

    /// <summary>
    /// Timestamp do snapshot
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Cria um snapshot para estado Off
    /// </summary>
    public static FocusModeSnapshot Off => new()
    {
        State = FocusModeState.Off,
        Mode = FocusModeType.None,
        RemainingMs = 0,
        CycleNumber = 0,
        TotalCyclesToday = 0,
        NextBreakType = BreakType.Short,
        AllowUserOverride = true,
    };
}

/// <summary>
/// Interface para o motor de modo de foco
///
/// SOLID:
/// - SRP: Apenas gerencia ciclos de foco/pausa
/// - ISP: Métodos coesos relacionados ao ciclo de foco
/// - DIP: Abstração para permitir diferentes implementações
///
/// OCP: Extensível para novos modos de foco
/// </summary>
public interface IFocusModeEngine
{
    /// <summary>
    /// Evento disparado quando o estado do engine muda
    /// </summary>
    event EventHandler<FocusModeStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Snapshot atual do estado do engine
    /// </summary>
    FocusModeSnapshot CurrentSnapshot { get; }

    /// <summary>
    /// Aplica uma nova política de modo de foco
    /// </summary>
    /// <param name="policy">Nova política a ser aplicada</param>
    void ApplyPolicy(FocusModePolicy policy);

    /// <summary>
    /// Inicia o motor de modo de foco manualmente
    /// </summary>
    /// <returns>True se iniciado com sucesso</returns>
    bool Start();

    /// <summary>
    /// Pausa o ciclo atual
    /// </summary>
    /// <returns>True se pausado com sucesso</returns>
    bool Pause();

    /// <summary>
    /// Retoma o ciclo pausado
    /// </summary>
    /// <returns>True se retomado com sucesso</returns>
    bool Resume();

    /// <summary>
    /// Para completamente o motor de modo de foco
    /// </summary>
    void Stop();

    /// <summary>
    /// Pula a pausa atual e inicia novo ciclo de foco
    /// </summary>
    /// <returns>True se pulado com sucesso</returns>
    bool SkipBreak();

    /// <summary>
    /// Obtém o snapshot atual do estado
    /// </summary>
    FocusModeSnapshot GetSnapshot();

    /// <summary>
    /// Obtém a política atual do engine
    /// </summary>
    FocusModePolicy GetPolicy();
}

/// <summary>
/// Argumentos do evento de mudança de estado
///
/// SRP: Apenas transporta dados da mudança de estado
/// </summary>
public sealed class FocusModeStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// Estado anterior
    /// </summary>
    public FocusModeState PreviousState { get; init; }

    /// <summary>
    /// Estado atual
    /// </summary>
    public FocusModeState CurrentState { get; init; }

    /// <summary>
    /// Snapshot atualizado
    /// </summary>
    public FocusModeSnapshot Snapshot { get; init; } = null!;

    /// <summary>
    /// Motivo da mudança de estado
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Timestamp da mudança
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
