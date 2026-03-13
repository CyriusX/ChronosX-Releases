namespace TimeTrack.Backend.Infrastructure.Jobs.Interfaces;

/// <summary>
/// Interface para o job de limpeza de dados auxiliares
/// Remove chaves de idempotência expiradas e outros dados temporários
/// </summary>
public interface ICleanupJob
{
    /// <summary>
    /// Executa a limpeza de dados expirados
    /// Método sem parâmetros para Hangfire
    /// </summary>
    Task ExecuteAsync();
}
