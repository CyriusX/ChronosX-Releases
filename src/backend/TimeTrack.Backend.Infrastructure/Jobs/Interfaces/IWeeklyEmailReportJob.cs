namespace TimeTrack.Backend.Infrastructure.Jobs.Interfaces;

/// <summary>
/// Interface para o job de envio de relatorio semanal por e-mail
///
/// ISP: Interface segregada com apenas operacoes necessarias
/// </summary>
public interface IWeeklyEmailReportJob
{
    /// <summary>
    /// Executa o job de relatorio semanal
    /// Verifica schedules que correspondem ao dia/hora atual e envia os e-mails
    /// Metodo para Hangfire (sem parametros opcionais)
    /// </summary>
    Task ExecuteAsync();
}
