namespace TimeTrack.Backend.Infrastructure.Jobs.Interfaces;

/// <summary>
/// Interface para o job de cálculo do Focus Score
///
/// ISP: Interface segregada com apenas operações necessárias
/// </summary>
public interface IFocusScoreJob
{
    /// <summary>
    /// Calcula os focus scores para uma data específica
    /// </summary>
    Task ExecuteForDateAsync(DateTime date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calcula os focus scores para os últimos N dias
    /// Método para Hangfire (sem parâmetros opcionais)
    /// </summary>
    Task ExecuteForRecentDaysAsync(int days);

    /// <summary>
    /// Recalcula o score de um usuário específico para uma data
    /// </summary>
    Task RecalculateUserScoreAsync(
        Guid userId,
        DateOnly date,
        CancellationToken cancellationToken = default);
}
