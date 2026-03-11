using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Infrastructure.Services;

/// <summary>
/// Implementação do contexto do usuário atual baseado em JWT
/// </summary>
public sealed class CurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var userIdClaim = User?.FindFirst(ClaimTypes.NameIdentifier)
                ?? User?.FindFirst("sub");

            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return userId;
            }

            return null;
        }
    }

    public Guid? OrgId
    {
        get
        {
            var orgIdClaim = User?.FindFirst("org_id");

            if (orgIdClaim != null && Guid.TryParse(orgIdClaim.Value, out var orgId))
            {
                return orgId;
            }

            return null;
        }
    }

    public Guid? DeviceId
    {
        get
        {
            var deviceIdClaim = User?.FindFirst("device_id");

            if (deviceIdClaim != null && Guid.TryParse(deviceIdClaim.Value, out var deviceId))
            {
                return deviceId;
            }

            return null;
        }
    }

    public UserRole? Role
    {
        get
        {
            var roleClaim = User?.FindFirst(ClaimTypes.Role)
                ?? User?.FindFirst("role");

            if (roleClaim != null && Enum.TryParse<UserRole>(roleClaim.Value, out var role))
            {
                return role;
            }

            return null;
        }
    }

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(UserRole role)
    {
        return Role == role;
    }
}
