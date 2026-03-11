namespace TimeTrack.Agent.Contracts.Services;

/// <summary>
/// Interface para armazenamento seguro de tokens JWT
/// </summary>
public interface ITokenStore
{
    /// <summary>
    /// Obtém o JWT armazenado
    /// </summary>
    Task<string?> GetJwtAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém o Refresh Token armazenado
    /// </summary>
    Task<string?> GetRefreshTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Armazena os tokens de forma segura
    /// </summary>
    Task StoreTokensAsync(string jwt, string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Limpa todos os tokens armazenados
    /// </summary>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se o JWT está expirando em breve
    /// </summary>
    bool IsJwtExpiringSoon(int withinMinutes = 5);

    /// <summary>
    /// Tenta renovar o token usando o refresh token
    /// </summary>
    /// <returns>True se renovado com sucesso, false se desativado ou token inválido</returns>
    Task<bool> RefreshAsync(CancellationToken cancellationToken = default);
}
