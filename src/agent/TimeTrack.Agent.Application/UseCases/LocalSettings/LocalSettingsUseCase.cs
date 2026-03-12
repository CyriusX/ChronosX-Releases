using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Domain.Entities;

namespace TimeTrack.Agent.Application.UseCases.LocalSettings;

/// <summary>
/// Use Case para gerenciar configurações locais do colaborador
///
/// SOLID:
/// - SRP: Apenas operações de configurações locais
/// - DIP: Depende de ILocalSettingsRepository (abstração)
/// - OCP: Extensível para novas configurações
///
/// Composition:
/// - Composição com ILocalSettingsRepository via construtor
/// </summary>
public sealed class LocalSettingsUseCase
{
    private readonly ILocalSettingsRepository _repository;

    public LocalSettingsUseCase(ILocalSettingsRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Obtém as configurações locais atuais
    /// </summary>
    public async Task<LocalSettingsResponse> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _repository.GetAsync(cancellationToken);
        return MapToResponse(settings);
    }

    /// <summary>
    /// Atualiza as configurações locais (atualização parcial)
    /// </summary>
    public async Task<LocalSettingsResponse> UpdateAsync(
        UpdateLocalSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        // Carregar configurações atuais
        var currentSettings = await _repository.GetAsync(cancellationToken);

        // Aplicar atualizações parciais usando o método de domínio
        var updatedSettings = currentSettings.WithUpdates(
            autoResumeNotification: request.AutoResumeNotificationEnabled,
            notificationSounds: request.NotificationSoundsEnabled,
            language: request.Language
        );

        // Persistir
        await _repository.SaveAsync(updatedSettings, cancellationToken);

        return MapToResponse(updatedSettings);
    }

    private static LocalSettingsResponse MapToResponse(Domain.Entities.LocalSettings settings)
    {
        return new LocalSettingsResponse
        {
            AutoResumeNotificationEnabled = settings.AutoResumeNotificationEnabled,
            NotificationSoundsEnabled = settings.NotificationSoundsEnabled,
            Language = settings.Language,
            UpdatedAt = settings.UpdatedAt
        };
    }
}
