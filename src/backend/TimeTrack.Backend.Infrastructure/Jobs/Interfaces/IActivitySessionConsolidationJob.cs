namespace TimeTrack.Backend.Infrastructure.Jobs.Interfaces;

/// <summary>
/// Job para consolidar sessões de atividade duplicadas no banco de dados
/// Após a sincronização com o agent, as sessões podem ter fragmentadas
/// devido a race conditions durante a captura
/// </summary>
public interface IActivitySessionConsolidationJob
{
    /// <summary>
    /// Executa a consolidação de sessões duplicadas para um período específico (12 horas)
    /// Método para Hangfire - sem parâmetros opcionais
    /// </summary>
    Task ExecuteConsolidationAsync();

    /// <summary>
    /// Executa a consolidação de sessões duplicadas para um período específico
    /// </summary>
    Task ExecuteAsync(TimeSpan period, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executa a consolidação para um usuário específico
    /// </summary>
    Task ExecuteForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
