using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.Common.Security;

/// <summary>
/// Interface para verificação de autorização de usuário
/// </summary>
public interface IUserAuthorizationService
{
    /// <summary>
    /// Verifica se o usuário atual pode acessar dados de outro usuário
    /// </summary>
    bool CanAccessUserData(Guid targetUserId);

    /// <summary>
    /// Verifica se o usuário atual tem uma das roles especificadas
    /// </summary>
    bool IsInAnyRole(params UserRole[] roles);

    /// <summary>
    /// Verifica se o usuário atual é Admin
    /// </summary>
    bool IsAdmin();

    /// <summary>
    /// Verifica se o usuário atual é Admin ou Gestor
    /// </summary>
    bool IsManagerOrAdmin();

    /// <summary>
    /// Garante que o usuário pode acessar dados de outro usuário, lança ForbiddenException se não
    /// </summary>
    void EnsureCanAccessUserData(Guid targetUserId);

    /// <summary>
    /// Garante que o usuário tem uma das roles, lança ForbiddenException se não
    /// </summary>
    void EnsureIsInAnyRole(params UserRole[] roles);
}

/// <summary>
/// Implementação do serviço de autorização de usuário
/// </summary>
public sealed class UserAuthorizationService : IUserAuthorizationService
{
    private readonly ICurrentUserContext _currentUser;

    public UserAuthorizationService(ICurrentUserContext currentUser)
    {
        _currentUser = currentUser;
    }

    public bool CanAccessUserData(Guid targetUserId)
    {
        // Usuário pode acessar seus próprios dados
        if (_currentUser.UserId == targetUserId)
            return true;

        // Admin e Gestor podem acessar dados de outros usuários da mesma org
        if (_currentUser.Role == UserRole.Admin || _currentUser.Role == UserRole.Gestor)
            return true;

        return false;
    }

    public bool IsInAnyRole(params UserRole[] roles)
    {
        if (!_currentUser.Role.HasValue)
            return false;

        return roles.Contains(_currentUser.Role.Value);
    }

    public bool IsAdmin()
    {
        return _currentUser.Role == UserRole.Admin;
    }

    public bool IsManagerOrAdmin()
    {
        return _currentUser.Role == UserRole.Admin || _currentUser.Role == UserRole.Gestor;
    }

    public void EnsureCanAccessUserData(Guid targetUserId)
    {
        if (!CanAccessUserData(targetUserId))
        {
            throw new Common.Exceptions.ForbiddenException(
                "You don't have permission to access this user's data");
        }
    }

    public void EnsureIsInAnyRole(params UserRole[] roles)
    {
        if (!IsInAnyRole(roles))
        {
            throw new Common.Exceptions.ForbiddenException(
                $"This action requires one of the following roles: {string.Join(", ", roles)}");
        }
    }
}
