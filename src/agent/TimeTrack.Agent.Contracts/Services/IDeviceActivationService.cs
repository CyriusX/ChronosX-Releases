namespace TimeTrack.Agent.Contracts.Services;

/// <summary>
/// Serviço responsável por ativar o dispositivo no backend
/// </summary>
public interface IDeviceActivationService
{
    /// <summary>
    /// Ativa o dispositivo no backend e atualiza os tokens com device_id
    /// </summary>
    /// <param name="jwt">JWT atual (sem device_id)</param>
    /// <param name="refreshToken">Refresh token atual</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Resultado da ativação com novos tokens</returns>
    Task<DeviceActivationResult> ActivateDeviceAsync(
        string jwt,
        string refreshToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se o dispositivo já está ativado
    /// </summary>
    bool IsDeviceActivated { get; }
}

/// <summary>
/// Resultado da ativação do dispositivo
/// </summary>
public sealed class DeviceActivationResult
{
    /// <summary>
    /// Indica se a ativação foi bem sucedida
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Novo JWT com device_id (se ativação bem sucedida)
    /// </summary>
    public string? AccessToken { get; init; }

    /// <summary>
    /// Novo refresh token (se ativação bem sucedida)
    /// </summary>
    public string? RefreshToken { get; init; }

    /// <summary>
    /// ID do dispositivo
    /// </summary>
    public Guid? DeviceId { get; init; }

    /// <summary>
    /// Mensagem de erro (se falhou)
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Status da ativação (activated, already_activated)
    /// </summary>
    public string? Status { get; init; }

    /// <summary>
    /// Cria um resultado de sucesso
    /// </summary>
    public static DeviceActivationResult Success(
        string accessToken,
        string refreshToken,
        Guid deviceId,
        string status) => new()
        {
            IsSuccess = true,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            DeviceId = deviceId,
            Status = status
        };

    /// <summary>
    /// Cria um resultado de falha
    /// </summary>
    public static DeviceActivationResult Failure(string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorMessage = errorMessage
    };
}
