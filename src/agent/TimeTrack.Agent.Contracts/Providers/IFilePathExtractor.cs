namespace TimeTrack.Agent.Contracts.Providers;

/// <summary>
/// Interface para extração do caminho do arquivo/pasta ativo
/// </summary>
public interface IFilePathExtractor
{
    /// <summary>
    /// Tenta extrair o caminho do arquivo ou pasta ativo na janela especificada
    /// </summary>
    /// <param name="windowHandle">Handle da janela</param>
    /// <param name="processName">Nome do processo (sem extensão)</param>
    /// <param name="windowTitle">Título da janela</param>
    /// <returns>Caminho do arquivo/pasta ou null se não for possível extrair</returns>
    string? ExtractFilePath(IntPtr windowHandle, string processName, string? windowTitle);
}
