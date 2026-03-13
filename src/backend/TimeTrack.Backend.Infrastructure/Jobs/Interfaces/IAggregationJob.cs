namespace TimeTrack.Backend.Infrastructure.Jobs.Interfaces;

/// <summary>
/// Interface para o job de agregação de dados
/// Calcula e persiste resumos diários pré-calculados para acelerar consultas
/// </summary>
public interface IAggregationJob
{
    /// <summary>
    /// Executa a agregação de dados para os últimos N dias
    /// Método para Hangfire (sem parâmetros opcionais)
    /// </summary>
    Task ExecuteForRecentDaysAsync(int days);
}
