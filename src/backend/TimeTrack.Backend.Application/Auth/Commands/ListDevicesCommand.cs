using MediatR;
using TimeTrack.Backend.Application.Auth.DTOs;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.Auth.Commands;

/// <summary>
/// Command para listar devices de uma organização
/// </summary>
public sealed record ListDevicesCommand(Guid OrgId) : IRequest<ListDevicesResponse>;

public sealed class ListDevicesCommandHandler : IRequestHandler<ListDevicesCommand, ListDevicesResponse>
{
    private readonly Domain.Interfaces.Repositories.IDeviceRepository _deviceRepository;
    private readonly ICurrentUserContext _currentUser;

    public ListDevicesCommandHandler(
        Domain.Interfaces.Repositories.IDeviceRepository deviceRepository,
        ICurrentUserContext currentUser)
    {
        _deviceRepository = deviceRepository;
        _currentUser = currentUser;
    }

    public async Task<ListDevicesResponse> Handle(ListDevicesCommand request, CancellationToken cancellationToken)
    {
        // Verify user belongs to the org
        if (_currentUser.OrgId != request.OrgId)
        {
            throw new ForbiddenException("Access denied to this organization");
        }

        var devices = await _deviceRepository.GetActiveByOrgIdAsync(request.OrgId, cancellationToken);

        var now = DateTime.UtcNow;
        var deviceList = devices.Select(d => new DeviceListItem
        {
            DeviceId = d.Id,
            Hostname = d.Hostname,
            DeviceName = d.DeviceName,
            AgentVersion = d.AgentVersion,
            DisplayMode = d.DisplayMode.ToString(),
            Status = CalculateStatus(d, now),
            TrackingState = d.TrackingState,
            HealthStatus = CalculateEffectiveHealth(d, now),
            LastSeenAt = d.LastHeartbeatAt,
            ActivatedAt = d.ActivatedAt,
            UserDisplayName = d.User?.DisplayName
        }).ToList();

        return new ListDevicesResponse
        {
            Devices = deviceList,
            TotalCount = deviceList.Count
        };
    }

    private static string CalculateStatus(Domain.Entities.Device device, DateTime now)
    {
        if (device.Status == DeviceStatus.Inactive)
            return "inactive";

        var lastSeen = device.LastHeartbeatAt ?? device.ActivatedAt;
        var timeSinceLastSeen = now - lastSeen;

        // Active if heartbeat in last 10 minutes
        if (timeSinceLastSeen.TotalMinutes <= 10)
            return "active";

        return "offline";
    }

    /// <summary>
    /// Returns effective health: "offline" if no recent heartbeat, otherwise
    /// the agent-reported health status, defaulting to null if never reported.
    /// </summary>
    private static string? CalculateEffectiveHealth(Domain.Entities.Device device, DateTime now)
    {
        if (device.Status == DeviceStatus.Inactive)
            return "inactive";

        var lastSeen = device.LastHeartbeatAt ?? device.ActivatedAt;
        if ((now - lastSeen).TotalMinutes > 10)
            return "offline";

        return device.HealthStatus; // healthy | degraded | unhealthy | null (never reported)
    }
}
