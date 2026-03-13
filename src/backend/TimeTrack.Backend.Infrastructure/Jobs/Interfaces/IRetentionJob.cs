namespace TimeTrack.Backend.Infrastructure.Jobs.Interfaces;

/// <summary>
/// Interface para o job de retenção de dados
/// Remove dados antigos baseado na política de retenção da organização
/// </summary>
public interface IRetentionJob
{
    /// <summary>
    /// Executa a limpeza de dados antigos para todas as organizações
    /// Método sem parâmetros para Hangfire
    /// </summary>
    Task ExecuteAsync();
}
