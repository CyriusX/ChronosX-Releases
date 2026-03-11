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
    private readonly ICurrentUserContext _currentUser;

    public ActivateDeviceCommandHandler(
        IDeviceRepository deviceRepository,
        ICurrentUserContext currentUser)
    {
        _deviceRepository = deviceRepository;
        _currentUser = currentUser;
    }

    public async Task<ActivateDeviceResponse> Handle(ActivateDeviceCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue || !_currentUser.OrgId.HasValue)
        {
            throw new ForbiddenException("User not authenticated");
        }

        // Check if device already exists
        var existingDevice = await _deviceRepository.GetByIdAsync(request.DeviceId, cancellationToken);

        if (existingDevice != null)
        {
            // Device already activated, update info
            existingDevice.RecordHeartbeat(request.AgentVersion);
            await _deviceRepository.UpdateAsync(existingDevice, cancellationToken);

            return new ActivateDeviceResponse
            {
                DeviceId = existingDevice.Id,
                ActivatedAt = existingDevice.ActivatedAt,
                Status = "already_activated"
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

        return new ActivateDeviceResponse
        {
            DeviceId = device.Id,
            ActivatedAt = device.ActivatedAt,
            Status = "activated"
        };
    }
}
