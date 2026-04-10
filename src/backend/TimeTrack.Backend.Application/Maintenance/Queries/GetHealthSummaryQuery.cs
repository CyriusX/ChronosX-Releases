using MediatR;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Maintenance.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.Maintenance.Queries;

public sealed record GetHealthSummaryQuery(Guid OrgId) : IRequest<HealthSummaryResponse>;

public sealed class GetHealthSummaryQueryHandler
    : IRequestHandler<GetHealthSummaryQuery, HealthSummaryResponse>
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly ICurrentUserContext _currentUser;

    public GetHealthSummaryQueryHandler(
        IDeviceRepository deviceRepository,
        ICurrentUserContext currentUser)
    {
        _deviceRepository = deviceRepository;
        _currentUser = currentUser;
    }

    public async Task<HealthSummaryResponse> Handle(
        GetHealthSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var devices = await _deviceRepository.GetActiveByOrgIdAsync(request.OrgId, cancellationToken);
        var now = DateTime.UtcNow;

        var alerts = new List<HealthAlertItem>();
        int onlineCount = 0, offlineCount = 0, degradedCount = 0, unhealthyCount = 0;

        foreach (var d in devices)
        {
            if (d.Status == DeviceStatus.Inactive) continue;

            var lastSeen = d.LastHeartbeatAt ?? d.ActivatedAt;
            var isOffline = (now - lastSeen).TotalMinutes > 10;

            if (isOffline)
            {
                offlineCount++;
                alerts.Add(new HealthAlertItem
                {
                    DeviceId = d.Id,
                    Hostname = d.Hostname,
                    UserDisplayName = d.User?.DisplayName,
                    Issue = "offline",
                    LastSeenAt = d.LastHeartbeatAt,
                    HealthStatus = "offline"
                });
            }
            else
            {
                onlineCount++;
                var health = d.HealthStatus;

                if (health == "unhealthy")
                {
                    unhealthyCount++;
                    alerts.Add(new HealthAlertItem
                    {
                        DeviceId = d.Id,
                        Hostname = d.Hostname,
                        UserDisplayName = d.User?.DisplayName,
                        Issue = "unhealthy",
                        LastSeenAt = d.LastHeartbeatAt,
                        HealthStatus = health
                    });
                }
                else if (health == "degraded")
                {
                    degradedCount++;
                    alerts.Add(new HealthAlertItem
                    {
                        DeviceId = d.Id,
                        Hostname = d.Hostname,
                        UserDisplayName = d.User?.DisplayName,
                        Issue = "degraded",
                        LastSeenAt = d.LastHeartbeatAt,
                        HealthStatus = health
                    });
                }
            }
        }

        return new HealthSummaryResponse
        {
            TotalDevices = onlineCount + offlineCount,
            OnlineCount = onlineCount,
            OfflineCount = offlineCount,
            DegradedCount = degradedCount,
            UnhealthyCount = unhealthyCount,
            Alerts = alerts
        };
    }
}
