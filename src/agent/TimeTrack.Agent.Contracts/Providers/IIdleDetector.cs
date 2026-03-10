namespace TimeTrack.Agent.Contracts.Providers;

/// <summary>
/// Interface para detecção de inatividade do usuário
/// </summary>
public interface IIdleDetector
{
    /// <summary>
    /// Obtém o tempo de inatividade do usuário
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Tempo de inatividade ou null se não disponível</returns>
    Task<TimeSpan?> GetIdleTimeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se o usuário está inativo
    /// </summary>
    /// <param name="threshold">Limiar de inatividade</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se inativo por mais que o limiar</returns>
    Task<bool> IsIdleAsync(TimeSpan threshold, CancellationToken cancellationToken = default);
}

/// <summary>
/// Evento de mudança de estado de inatividade
/// </summary>
public sealed class IdleStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// Indica se o usuário está inativo
    /// </summary>
    public bool IsIdle { get; init; }

    /// <summary>
    /// Tempo de inatividade atual
    /// </summary>
    public TimeSpan IdleTime { get; init; }

    /// <summary>
    /// Momento da mudança
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
