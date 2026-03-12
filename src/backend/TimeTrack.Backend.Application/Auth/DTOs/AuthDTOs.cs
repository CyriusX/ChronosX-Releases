using System.ComponentModel.DataAnnotations;

namespace TimeTrack.Backend.Application.Auth.DTOs;

/// <summary>
/// Request para login
/// </summary>
public sealed class LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

/// <summary>
/// Response do login
/// </summary>
public sealed class LoginResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public string TokenType { get; init; } = "Bearer";
    public int ExpiresIn { get; init; }
    public Guid UserId { get; init; }
    public Guid OrgId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string OrgName { get; init; } = string.Empty;
    public bool PasswordMustChange { get; init; }
}

/// <summary>
/// Request para refresh token
/// </summary>
public sealed class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; init; } = string.Empty;
}

/// <summary>
/// Response do refresh token
/// </summary>
public sealed class RefreshTokenResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public string TokenType { get; init; } = "Bearer";
    public int ExpiresIn { get; init; }
}

/// <summary>
/// Request para ativação de device
/// </summary>
public sealed class ActivateDeviceRequest
{
    [Required]
    public Guid DeviceId { get; init; }

    [Required]
    public string Hostname { get; init; } = string.Empty;

    public string? DeviceName { get; init; }

    [Required]
    public string AgentVersion { get; init; } = string.Empty;

    [Required]
    public string DisplayMode { get; init; } = "background";
}

/// <summary>
/// Response da ativação de device
/// </summary>
public sealed class ActivateDeviceResponse
{
    public Guid DeviceId { get; init; }
    public DateTime ActivatedAt { get; init; }
    public string Status { get; init; } = string.Empty;

    // Tokens for Agent to use (device-linked refresh token)
    public string? AccessToken { get; init; }
    public string? RefreshToken { get; init; }
    public int ExpiresIn { get; init; }
}

/// <summary>
/// Request para logout
/// </summary>
public sealed class LogoutRequest
{
    public string? RefreshToken { get; init; }
}

/// <summary>
/// Request para heartbeat do device
/// </summary>
public sealed class HeartbeatRequest
{
    public string? AgentVersion { get; init; }
}

/// <summary>
/// Response do heartbeat
/// </summary>
public sealed class HeartbeatResponse
{
    public DateTime LastSeenAt { get; init; }
    public string Status { get; init; } = string.Empty;
}

/// <summary>
/// Response da listagem de devices
/// </summary>
public sealed class DeviceListItem
{
    public Guid DeviceId { get; init; }
    public string Hostname { get; init; } = string.Empty;
    public string? DeviceName { get; init; }
    public string AgentVersion { get; init; } = string.Empty;
    public string DisplayMode { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime? LastSeenAt { get; init; }
    public DateTime ActivatedAt { get; init; }
}

/// <summary>
/// Response da listagem de devices da org
/// </summary>
public sealed class ListDevicesResponse
{
    public List<DeviceListItem> Devices { get; init; } = [];
    public int TotalCount { get; init; }
}

/// <summary>
/// Request para registro (B2C)
/// </summary>
public sealed class RegisterRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; init; } = string.Empty;

    [Required]
    public string DisplayName { get; init; } = string.Empty;

    [Required]
    public string OrganizationName { get; init; } = string.Empty;
}

/// <summary>
/// Response do registro
/// </summary>
public sealed class RegisterResponse
{
    public Guid UserId { get; init; }
    public Guid OrgId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string OrganizationName { get; init; } = string.Empty;
}

/// <summary>
/// Request para convite de usuário (B2B)
/// </summary>
public sealed class InviteUserRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string DisplayName { get; init; } = string.Empty;

    [Required]
    public string Role { get; init; } = string.Empty;
}

/// <summary>
/// Response do convite
/// </summary>
public sealed class InviteUserResponse
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string TemporaryPassword { get; init; } = string.Empty;
}

/// <summary>
/// Request para forgot password
/// </summary>
public sealed class ForgotPasswordRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;
}

/// <summary>
/// Response do forgot password
/// </summary>
public sealed class ForgotPasswordResponse
{
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Request para reset password
/// </summary>
public sealed class ResetPasswordRequest
{
    [Required]
    public string Token { get; init; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string NewPassword { get; init; } = string.Empty;
}

/// <summary>
/// Response do reset password
/// </summary>
public sealed class ResetPasswordResponse
{
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Request para change password
/// </summary>
public sealed class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; init; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string NewPassword { get; init; } = string.Empty;
}

/// <summary>
/// Response do change password
/// </summary>
public sealed class ChangePasswordResponse
{
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Request para atualizar status de membro
/// </summary>
public sealed class UpdateMemberStatusRequest
{
    [Required]
    public Guid UserId { get; init; }

    [Required]
    public string Status { get; init; } = string.Empty; // "active" or "inactive"
}

/// <summary>
/// Request para atualizar role de membro
/// </summary>
public sealed class UpdateMemberRoleRequest
{
    [Required]
    public Guid UserId { get; init; }

    [Required]
    public string Role { get; init; } = string.Empty; // "Colaborador", "Gestor", "Admin"
}
