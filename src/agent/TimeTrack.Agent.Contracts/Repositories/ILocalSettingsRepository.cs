using TimeTrack.Agent.Domain.Entities;

namespace TimeTrack.Agent.Contracts.Repositories;

/// <summary>
/// Interface para persistência de configurações locais
///
/// SOLID:
/// - ISP: Interface segregada com apenas operações necessárias
/// - DIP: Abstração para injeção de dependência
/// </summary>
public interface ILocalSettingsRepository
{
    /// <summary>
    /// Obtém as configurações locais (cria com defaults se não existir)
    /// </summary>
    Task<LocalSettings> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Salva as configurações locais
    /// </summary>
    Task SaveAsync(LocalSettings settings, CancellationToken cancellationToken = default);
}
