using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Api.Security;

/// <summary>
/// Handler para verificação de roles baseado em claims
/// </summary>
public sealed class RoleAuthorizationHandler : AuthorizationHandler<RoleRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RoleRequirement requirement)
    {
        var roleClaim = context.User.FindFirst(ClaimTypes.Role)
            ?? context.User.FindFirst("role");

        if (roleClaim != null)
        {
            if (Enum.TryParse<UserRole>(roleClaim.Value, out var userRole))
            {
                if (requirement.AllowedRoles.Contains(userRole))
                {
                    context.Succeed(requirement);
                }
            }
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Requirement para autorização baseada em roles
/// </summary>
public sealed class RoleRequirement : IAuthorizationRequirement
{
    public IReadOnlySet<UserRole> AllowedRoles { get; }

    public RoleRequirement(params UserRole[] allowedRoles)
    {
        AllowedRoles = allowedRoles.ToHashSet();
    }
}

/// <summary>
/// Policy names for authorization
/// </summary>
public static class AuthorizationPolicies
{
    public const string AdminOnly = "AdminOnly";
    public const string ManagerOrAdmin = "ManagerOrAdmin";
    public const string Authenticated = "Authenticated";
    public const string PlatformAdminOnly = "PlatformAdminOnly";

    public static void Configure(AuthorizationOptions options)
    {
        options.AddPolicy(AdminOnly, policy =>
            policy.RequireRole(UserRole.Admin.ToString()));

        options.AddPolicy(ManagerOrAdmin, policy =>
            policy.RequireRole(
                UserRole.Admin.ToString(),
                UserRole.Gestor.ToString()));

        options.AddPolicy(Authenticated, policy =>
            policy.RequireAuthenticatedUser());

        options.AddPolicy(PlatformAdminOnly, policy =>
            policy.RequireClaim("is_platform_admin", "true"));
    }
}
