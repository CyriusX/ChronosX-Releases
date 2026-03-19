using TimeTrack.Agent.Contracts.Services;

namespace TimeTrack.Agent.Infrastructure.Services;

/// <summary>
/// Implementação de IDeviceActivationService para modo local/teste (sem backend).
/// Sempre retorna falha pois não há backend disponível para ativar dispositivos.
/// </summary>
public sealed class NullDeviceActivationService : IDeviceActivationService
{
    public bool IsDeviceActivated => false;

    public Task<DeviceActivationResult> ActivateDeviceAsync(
        string jwt,
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(DeviceActivationResult.Failure("No backend configured for device activation."));
    }
}
