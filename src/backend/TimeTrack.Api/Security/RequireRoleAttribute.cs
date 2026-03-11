using Microsoft.AspNetCore.Authorization;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Api.Security;

/// <summary>
/// Atributo de autorização que exige roles específicas
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequireRoleAttribute : AuthorizeAttribute
{
    public RequireRoleAttribute(params UserRole[] roles)
    {
        Roles = string.Join(",", roles.Select(r => r.ToString()));
    }

    public RequireRoleAttribute(params string[] roles)
    {
        Roles = string.Join(",", roles);
    }
}

/// <summary>
/// Atributo que exige que o usuário seja Admin
/// </summary>
public class RequireAdminAttribute : RequireRoleAttribute
{
    public RequireAdminAttribute() : base(UserRole.Admin) { }
}

/// <summary>
/// Atributo que exige que o usuário seja Admin ou Gestor
/// </summary>
public class RequireManagerAttribute : RequireRoleAttribute
{
    public RequireManagerAttribute() : base(UserRole.Admin, UserRole.Gestor) { }
}

/// <summary>
/// Atributo que exige que o usuário acesse apenas seus próprios dados
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireOwnerOrRoleAttribute : Attribute
{
    public UserRole[] AllowedRoles { get; }

    public RequireOwnerOrRoleAttribute(params UserRole[] allowedRoles)
    {
        AllowedRoles = allowedRoles;
    }

    public RequireOwnerOrRoleAttribute()
    {
        AllowedRoles = new[] { UserRole.Admin, UserRole.Gestor };
    }
}
