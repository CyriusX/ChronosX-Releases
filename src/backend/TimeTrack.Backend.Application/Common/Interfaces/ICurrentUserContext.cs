using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.Common.Interfaces;

/// <summary>
/// Interface para resolver o contexto do usuário atual
/// </summary>
public interface ICurrentUserContext
{
    Guid? UserId { get; }
    Guid? OrgId { get; }
    Guid? DeviceId { get; }
    UserRole? Role { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(UserRole role);
}
