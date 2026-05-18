using MediatR;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Maintenance.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Maintenance.Queries;

public sealed record GetDeviceInfoQuery(Guid OrgId, Guid DeviceId) : IRequest<DeviceInfoResponse?>;

public sealed class GetDeviceInfoQueryHandler
    : IRequestHandler<GetDeviceInfoQuery, DeviceInfoResponse?>
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly ICurrentUserContext _currentUser;

    public GetDeviceInfoQueryHandler(
        IDeviceRepository deviceRepository,
        ICurrentUserContext currentUser)
    {
        _deviceRepository = deviceRepository;
        _currentUser = currentUser;
    }

    public async Task<DeviceInfoResponse?> Handle(
        GetDeviceInfoQuery request,
        CancellationToken cancellationToken)
    {
        // Platform admins can access any organization
        if (!_currentUser.IsPlatformAdmin && _currentUser.OrgId != request.OrgId)
            return null;

        var device = await _deviceRepository.GetByIdAsync(request.DeviceId, cancellationToken);
        if (device is null)
            return null;

        var now = DateTime.UtcNow;
        var lastSeen = device.LastHeartbeatAt ?? device.ActivatedAt;
        var status = device.Status == Domain.ValueObjects.DeviceStatus.Inactive ? "inactive"
            : (now - lastSeen).TotalMinutes <= 10 ? "active"
            : "offline";

        return new DeviceInfoResponse
        {
            DeviceId = device.Id,
            UserId = device.UserId,
            Hostname = device.Hostname,
            DeviceName = device.DeviceName,
            AgentVersion = device.AgentVersion,
            OsVersion = device.OsVersion,
            IpAddress = device.IpAddress,
            UptimeSeconds = device.UptimeSeconds,
            TrackingState = device.TrackingState,
            HealthStatus = device.HealthStatus,
            ConsecutiveSyncFailures = device.ConsecutiveSyncFailures,
            LastSuccessfulSyncAt = device.LastSuccessfulSyncAt,
            IpcConnected = device.IpcConnected,
            LastHeartbeatAt = device.LastHeartbeatAt,
            ActivatedAt = device.ActivatedAt,
            Status = status,
            DisplayMode = device.DisplayMode.ToString(),
            UserDisplayName = device.User?.DisplayName,
            DevToolsEnabledUntilUtc = device.User?.DevToolsEnabledUntilUtc
        };
    }
}
