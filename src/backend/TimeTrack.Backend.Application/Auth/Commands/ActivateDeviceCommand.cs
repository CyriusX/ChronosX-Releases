using MediatR;
using TimeTrack.Backend.Application.Auth.DTOs;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.Auth.Commands;

/// <summary>
/// Command para ativar um dispositivo
/// </summary>
public sealed record ActivateDeviceCommand(
    Guid DeviceId,
    string Hostname,
    string? DeviceName,
    string AgentVersion,
    string DisplayMode) : IRequest<ActivateDeviceResponse>;

public sealed class ActivateDeviceCommandHandler : IRequestHandler<ActivateDeviceCommand, ActivateDeviceResponse>
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITokenService _tokenService;
    private readonly ICurrentUserContext _currentUser;
    private readonly IAuditLogService _auditLogService;

    public ActivateDeviceCommandHandler(
        IDeviceRepository deviceRepository,
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        ITokenService tokenService,
        ICurrentUserContext currentUser,
        IAuditLogService auditLogService)
    {
        _deviceRepository = deviceRepository;
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _tokenService = tokenService;
        _currentUser = currentUser;
        _auditLogService = auditLogService;
    }

    public async Task<ActivateDeviceResponse> Handle(ActivateDeviceCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue || !_currentUser.OrgId.HasValue)
        {
            throw new ForbiddenException("User not authenticated");
        }

        // Get current user
        var user = await _userRepository.GetByIdAsync(_currentUser.UserId.Value, cancellationToken);
        if (user == null || user.Status == UserStatus.Inactive)
        {
            throw new ForbiddenException("User account is deactivated");
        }

        // Check if device already exists
        var existingDevice = await _deviceRepository.GetByIdAsync(request.DeviceId, cancellationToken);

        if (existingDevice != null)
        {
            // Device already activated, update info
            existingDevice.RecordHeartbeat(request.AgentVersion);
            await _deviceRepository.UpdateAsync(existingDevice, cancellationToken);

            // Generate new tokens for re-activation (include device_id in JWT)
            var accessToken = _tokenService.GenerateAccessToken(user.Id, user.OrgId, existingDevice.Id, user.Role.ToString(), user.PasswordMustChange);
            var (refreshToken, _) = await CreateDeviceRefreshTokenAsync(user.Id, existingDevice.Id, cancellationToken);

            // Audit log - device.reactivated
            _auditLogService.LogAsync(
                AuditActions.DeviceReactivated,
                "device",
                existingDevice.Id,
                new { hostname = request.Hostname, agentVersion = request.AgentVersion },
                cancellationToken);

            return new ActivateDeviceResponse
            {
                DeviceId = existingDevice.Id,
                ActivatedAt = existingDevice.ActivatedAt,
                Status = "already_activated",
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = (int)_tokenService.GetAccessTokenExpiration().TotalSeconds
            };
        }

        // Parse display mode
        var displayMode = request.DisplayMode.ToLowerInvariant() switch
        {
            "foreground" => DisplayMode.Foreground,
            _ => DisplayMode.Background
        };

        // Create new device
        var device = Domain.Entities.Device.Create(
            request.DeviceId,
            _currentUser.OrgId.Value,
            _currentUser.UserId.Value,
            request.Hostname,
            request.AgentVersion,
            displayMode,
            request.DeviceName
        );

        await _deviceRepository.AddAsync(device, cancellationToken);

        // Generate tokens linked to this device (include device_id in JWT)
        var newAccessToken = _tokenService.GenerateAccessToken(user.Id, user.OrgId, device.Id, user.Role.ToString(), user.PasswordMustChange);
        var (newRefreshToken, _) = await CreateDeviceRefreshTokenAsync(user.Id, device.Id, cancellationToken);

        // Audit log - device.registered
        _auditLogService.LogAsync(
            AuditActions.DeviceRegistered,
            "device",
            device.Id,
            new { hostname = request.Hostname, deviceName = request.DeviceName, displayMode = displayMode.ToString(), agentVersion = request.AgentVersion },
            cancellationToken);

        return new ActivateDeviceResponse
        {
            DeviceId = device.Id,
            ActivatedAt = device.ActivatedAt,
            Status = "activated",
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            ExpiresIn = (int)_tokenService.GetAccessTokenExpiration().TotalSeconds
        };
    }

    private async Task<(string Token, Guid Id)> CreateDeviceRefreshTokenAsync(
        Guid userId,
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        var refreshToken = _tokenService.GenerateRefreshToken();
        var refreshTokenHash = _tokenService.HashRefreshToken(refreshToken);

        var tokenEntity = Domain.Entities.RefreshToken.Create(
            userId,
            deviceId,
            refreshTokenHash,
            _tokenService.GetRefreshTokenExpiration()
        );

        await _refreshTokenRepository.AddAsync(tokenEntity, cancellationToken);

        return (refreshToken, tokenEntity.Id);
    }
}
