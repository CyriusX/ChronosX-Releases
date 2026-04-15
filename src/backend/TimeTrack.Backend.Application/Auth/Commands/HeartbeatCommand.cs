using MediatR;
using TimeTrack.Backend.Application.Auth.DTOs;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Auth.Commands;

/// <summary>
/// Command para registrar heartbeat do device
/// </summary>
public sealed record HeartbeatCommand(
    Guid DeviceId,
    string? AgentVersion,
    string? OsVersion = null,
    string? IpAddress = null,
    int? UptimeSeconds = null,
    string? TrackingState = null,
    string? HealthStatus = null,
    bool? BackendReachable = null,
    int? ConsecutiveSyncFailures = null,
    DateTime? LastSuccessfulSyncAt = null,
    bool? IpcConnected = null) : IRequest<HeartbeatResponse>;

public sealed class HeartbeatCommandHandler : IRequestHandler<HeartbeatCommand, HeartbeatResponse>
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IRemoteCommandRepository _remoteCommandRepository;

    public HeartbeatCommandHandler(
        IDeviceRepository deviceRepository,
        IRemoteCommandRepository remoteCommandRepository)
    {
        _deviceRepository = deviceRepository;
        _remoteCommandRepository = remoteCommandRepository;
    }

    public async Task<HeartbeatResponse> Handle(HeartbeatCommand request, CancellationToken cancellationToken)
    {
        var device = await _deviceRepository.GetByIdAsync(request.DeviceId, cancellationToken);

        if (device is null)
        {
            throw new NotFoundException("Device", request.DeviceId);
        }

        device.RecordHeartbeat(
            request.AgentVersion,
            request.OsVersion,
            request.IpAddress,
            request.UptimeSeconds,
            request.TrackingState,
            request.HealthStatus,
            request.BackendReachable,
            request.ConsecutiveSyncFailures,
            request.LastSuccessfulSyncAt,
            request.IpcConnected);
        await _deviceRepository.UpdateAsync(device, cancellationToken);

        // Check if there are pending commands for this device
        var pendingCommands = await _remoteCommandRepository.GetPendingByDeviceIdAsync(
            request.DeviceId, cancellationToken);

        return new HeartbeatResponse
        {
            LastSeenAt = device.LastHeartbeatAt ?? DateTime.UtcNow,
            Status = device.Status.ToString(),
            HasPendingCommands = pendingCommands.Any()
        };
    }
}
