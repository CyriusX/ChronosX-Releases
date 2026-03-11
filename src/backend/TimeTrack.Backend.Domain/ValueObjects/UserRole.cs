namespace TimeTrack.Backend.Domain.ValueObjects;

/// <summary>
/// Role do usuário no sistema
/// </summary>
public enum UserRole
{
    /// <summary>
    /// Colaborador - apenas próprios dados
    /// </summary>
    Colaborador = 1,

    /// <summary>
    /// Gestor - relatórios + políticas da org
    /// </summary>
    Gestor = 2,

    /// <summary>
    /// Admin - tudo na organização
    /// </summary>
    Admin = 3
}
