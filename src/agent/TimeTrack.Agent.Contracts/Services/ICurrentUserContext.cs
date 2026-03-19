namespace TimeTrack.Agent.Contracts.Services;

/// <summary>
/// Contexto do usuário atualmente autenticado no Agent
///
/// Responsável por:
/// - Extrair user_id do JWT armazenado
/// - Fornecer contexto para repositórios filtrarem dados
/// - Detectar mudança de usuário para limpeza de cache
/// </summary>
public interface ICurrentUserContext
{
    /// <summary>
    /// ID do usuário atualmente autenticado
    /// Retorna null se não houver usuário autenticado
    /// </summary>
    Guid? UserId { get; }

    /// <summary>
    /// ID da organização do usuário atual
    /// Retorna null se não houver usuário autenticado
    /// </summary>
    Guid? OrgId { get; }

    /// <summary>
    /// ID do dispositivo extraído do claim device_id no JWT.
    /// Retorna null se o token não contiver o claim (gerado antes de ActivateDevice).
    /// </summary>
    Guid? DeviceId { get; }

    /// <summary>
    /// Indica se há um usuário autenticado
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Atualiza o contexto com base no token atual
    /// Deve ser chamado após login ou refresh do token
    /// </summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Limpa o contexto (logout)
    /// </summary>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Evento disparado quando o usuário muda
    /// Útil para limpar caches locais
    /// </summary>
    event EventHandler<UserChangedEventArgs>? UserChanged;
}

/// <summary>
/// Argumentos do evento de mudança de usuário
/// </summary>
public sealed class UserChangedEventArgs : EventArgs
{
    public Guid? PreviousUserId { get; init; }
    public Guid? NewUserId { get; init; }

    public bool UserChanged => PreviousUserId != NewUserId;
}
