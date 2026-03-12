namespace TimeTrack.Backend.Application.Common.Interfaces;

/// <summary>
/// Interface para geração de senhas temporárias
/// </summary>
public interface IPasswordGenerator
{
    /// <summary>
    /// Gera uma senha temporária segura
    /// </summary>
    /// <param name="length">Tamanho da senha (padrão: 12)</param>
    /// <returns>Senha gerada</returns>
    string GenerateTemporaryPassword(int length = 12);
}
