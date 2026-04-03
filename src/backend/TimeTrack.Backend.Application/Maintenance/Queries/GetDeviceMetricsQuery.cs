using MediatR;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Maintenance.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Maintenance.Queries;

/// <summary>
/// Query para obter métricas de máquina de um dispositivo
/// </summary>
public sealed record GetDeviceMetricsQuery(Guid OrgId, Guid DeviceId) : IRequest<DeviceMetricsResponse?>;

/// <summary>
/// Handler para obtenção de métricas de máquina
/// </summary>
public sealed class GetDeviceMetricsQueryHandler : IRequestHandler<GetDeviceMetricsQuery, DeviceMetricsResponse?>
{
    private readonly IMachineMetricsRepository _metricsRepository;
    private readonly ICurrentUserContext _currentUser;

    public GetDeviceMetricsQueryHandler(
        IMachineMetricsRepository metricsRepository,
        ICurrentUserContext currentUser)
    {
        _metricsRepository = metricsRepository;
        _currentUser = currentUser;
    }

    public async Task<DeviceMetricsResponse?> Handle(
        GetDeviceMetricsQuery request,
        CancellationToken cancellationToken)
    {
        // Multi-tenancy: ensure org matches
        if (_currentUser.OrgId != request.OrgId)
            return null;

        var latest = await _metricsRepository.GetLatestByDeviceIdAsync(
            request.DeviceId, cancellationToken);

        if (latest == null)
        {
            return new DeviceMetricsResponse
            {
                DeviceId = request.DeviceId,
                LastUpdatedAt = null
            };
        }

        // Get last 30 minutes of history for sparklines
        var since = DateTime.UtcNow.AddMinutes(-30);
        var history = await _metricsRepository.GetByDeviceIdSinceAsync(
            request.DeviceId, since, cancellationToken);

        return new DeviceMetricsResponse
        {
            DeviceId = request.DeviceId,
            CpuPercent = latest.CpuPercent,
            MemoryUsedMb = latest.MemoryUsedMb,
            MemoryTotalMb = latest.MemoryTotalMb,
            DiskUsedGb = latest.DiskUsedGb,
            DiskTotalGb = latest.DiskTotalGb,
            LastUpdatedAt = latest.SampledAtUtc,
            RecentHistory = history.Select(m => new MetricsHistoryPoint
            {
                SampledAt = m.SampledAtUtc,
                CpuPercent = m.CpuPercent,
                MemoryUsedMb = m.MemoryUsedMb
            }).ToList()
        };
    }
}
