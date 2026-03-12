namespace TimeTrack.Backend.Application.Common.Interfaces;

/// <summary>
/// Interface para serviço de auditoria
/// </summary>
public interface IAuditLogService
{
    /// <summary>
    /// Registra uma ação de auditoria de forma assíncrona (fire-and-forget)
    /// Usa o contexto do usuário atual (ICurrentUserContext)
    /// </summary>
    void LogAsync(
        string action,
        string entityType,
        Guid? entityId = null,
        object? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra uma ação de auditoria e aguarda a conclusão
    /// Usa o contexto do usuário atual (ICurrentUserContext)
    /// </summary>
    Task LogAsync(
        string action,
        string entityType,
        Guid? entityId,
        object? metadata,
        bool waitForCompletion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra uma ação de auditoria com contexto HTTP explícito
    /// Usa o contexto do usuário atual (ICurrentUserContext)
    /// </summary>
    Task LogWithHttpContextAsync(
        string action,
        string entityType,
        Guid? entityId = null,
        object? metadata = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra uma ação de auditoria com contexto explícito (para operações sem usuário logado)
    /// </summary>
    Task LogExplicitAsync(
        Guid orgId,
        Guid? userId,
        string action,
        string entityType,
        Guid? entityId = null,
        object? metadata = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Constantes de ações de auditoria
/// </summary>
public static class AuditActions
{
    public const string UserLogin = "user.login";
    public const string UserLogout = "user.logout";
    public const string UserInviteAccepted = "user.invite_accepted";
    public const string UserRemoved = "user.removed";
    public const string UserReactivated = "user.reactivated";
    public const string PolicyUpdated = "policy.updated";
    public const string ReportAccessed = "report.accessed";
    public const string DeviceRegistered = "device.registered";
    public const string DeviceReactivated = "device.reactivated";
    public const string MemberRoleChanged = "member.role_changed";
}
