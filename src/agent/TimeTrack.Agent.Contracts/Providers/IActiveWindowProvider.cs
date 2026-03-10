using TimeTrack.Agent.Domain.Entities;

namespace TimeTrack.Agent.Contracts.Providers;

/// <summary>
/// Interface para obtenção da janela ativa no Windows
/// </summary>
public interface IActiveWindowProvider
{
    /// <summary>
    /// Obtém informações da janela ativa atual
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Informações da janela ou null se não houver</returns>
    Task<ActiveWindowInfo?> GetActiveWindowAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Informações da janela ativa
/// </summary>
public sealed record ActiveWindowInfo
{
    /// <summary>
    /// Hash do caminho do executável
    /// </summary>
    public required string ExePathHash { get; init; }

    /// <summary>
    /// Nome de exibição da aplicação
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Caminho completo do executável
    /// </summary>
    public string? ExePath { get; init; }

    /// <summary>
    /// Título da janela
    /// </summary>
    public string? WindowTitle { get; init; }

    /// <summary>
    /// Hash da janela para agrupamento
    /// </summary>
    public string? WindowHash { get; init; }
}
