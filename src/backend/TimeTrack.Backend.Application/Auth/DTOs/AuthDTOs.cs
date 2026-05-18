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
    public string Role { get; init; } = string.Empty;
    public string OrgName { get; init; } = string.Empty;
    public bool PasswordMustChange { get; init; }
    public string SubscriptionStatus { get; init; } = "none";
    public string PlanTier { get; init; } = "";
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
    public string SubscriptionStatus { get; init; } = "none";
    public string PlanTier { get; init; } = "";
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
    public string? OsVersion { get; init; }
    public string? IpAddress { get; init; }
    public int? UptimeSeconds { get; init; }
    public string? TrackingState { get; init; }
    public string? HealthStatus { get; init; }
    public bool? BackendReachable { get; init; }
    public int? ConsecutiveSyncFailures { get; init; }
    public DateTime? LastSuccessfulSyncAt { get; init; }
    public bool? IpcConnected { get; init; }
}

/// <summary>
/// Response do heartbeat
/// </summary>
public sealed class HeartbeatResponse
{
    public DateTime LastSeenAt { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool HasPendingCommands { get; init; }
    public string SubscriptionStatus { get; init; } = "active";
    public DateTime? GracePeriodEnd { get; init; }
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
    public string? TrackingState { get; init; }
    public string? HealthStatus { get; init; }
    public bool? IpcConnected { get; init; }
    public DateTime? LastSeenAt { get; init; }
    public DateTime ActivatedAt { get; init; }
    public string? UserDisplayName { get; init; }
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

/// <summary>
/// Item de membro na listagem
/// </summary>
public sealed class MemberListItem
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// Response da listagem de membros
/// </summary>
public sealed class ListMembersResponse
{
    public List<MemberListItem> Members { get; init; } = [];
    public int TotalCount { get; init; }
}

/// <summary>
/// Item de membro com status de tempo no team status
/// </summary>
public sealed class TeamMemberStatusItem
{
    public Guid UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    /// <summary>
    /// Tempo trabalhado hoje em segundos
    /// </summary>
    public int TodayDurationSeconds { get; init; }
    /// <summary>
    /// Tempo trabalhado hoje formatado (ex: "2h 30m")
    /// </summary>
    public string TodayDurationFormatted { get; init; } = "0h 0m";
    /// <summary>
    /// Se o usuário está atualmente rastreando tempo
    /// </summary>
    public bool IsTracking { get; init; }
    /// <summary>
    /// Latest tracking state derived from device heartbeat: running | idle | paused | stopped | unknown | offline
    /// </summary>
    public string? TrackingState { get; init; }
    /// <summary>
    /// Timestamp of the most recent activity session for this user (UTC ISO 8601)
    /// </summary>
    public string? LastSyncAt { get; init; }
}

/// <summary>
/// Response do status da equipe
/// </summary>
public sealed class TeamStatusResponse
{
    public List<TeamMemberStatusItem> Members { get; init; } = [];
    public int TotalCount { get; init; }
    public int ActiveCount { get; init; }
    public int TrackingCount { get; init; }
}

/// <summary>
/// Response do resumo de um membro da equipe (mesma shape que TodaySummaryResponse do frontend)
/// </summary>
public sealed class TeamMemberSummaryResponse
{
    public long TotalDuration { get; init; }
    public long ProductiveTime { get; init; }
    public long IdleTime { get; init; }
    public long FocusTime { get; init; }
    public int FocusScore { get; init; }
    public int SessionsCount { get; init; }
    public List<MemberProjectSummary> TopProjects { get; init; } = [];
    public List<MemberTaskSummary> TopTasks { get; init; } = [];
    public List<MemberAppSummary> TopApplications { get; init; } = [];
    public List<MemberAppSummary>? TopAppsByExe { get; init; }
    public List<MemberCategorySummary> Categories { get; init; } = [];
    public List<MemberWeeklyHistoryItem> WeeklyHistory { get; init; } = [];
    /// <summary>
    /// Timestamp of the most recent synced activity session (UTC ISO 8601)
    /// </summary>
    public string? LastSyncAt { get; init; }
}

public sealed class MemberProjectSummary
{
    public Guid? ProjectId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Color { get; init; }
    public long Duration { get; init; }
    public double Percentage { get; init; }
}

public sealed class MemberTaskSummary
{
    public Guid TaskId { get; init; }
    public string Title { get; init; } = string.Empty;
    public Guid ProjectId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public string ProjectColor { get; init; } = "#4A9FFF";
    public long Duration { get; init; }
    public string Status { get; init; } = string.Empty;
}

public sealed class MemberAppSummary
{
    public string Name { get; init; } = string.Empty;
    public long Duration { get; init; }
    public double Percentage { get; init; }
    public string? Productivity { get; init; }
    public string? Subcategory { get; init; }
    public string? Source { get; init; }
}

public sealed class MemberCategorySummary
{
    public string Name { get; init; } = string.Empty;
    public long Duration { get; init; }
    public double Percentage { get; init; }
    public string Color { get; init; } = "#94a3b8";
    public string? Productivity { get; init; }
}

public sealed class MemberWeeklyHistoryItem
{
    public string Date { get; init; } = string.Empty;
    public string DayName { get; init; } = string.Empty;
    public double Hours { get; init; }
    public bool IsToday { get; init; }
}
