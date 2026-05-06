using MediatR;
using TimeTrack.Backend.Application.Auth.DTOs;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Auth.Commands;

public sealed record HeartbeatCommand(
    Guid DeviceId,
    string? AgentVersion,
    string? OsVersion,
    string? IpAddress,
    int? UptimeSeconds,
    string? TrackingState,
    string? HealthStatus,
    bool? BackendReachable,
    int? ConsecutiveSyncFailures,
    DateTime? LastSuccessfulSyncAt,
    bool? IpcConnected) : IRequest<HeartbeatResponse>;

public sealed class HeartbeatCommandHandler : IRequestHandler<HeartbeatCommand, HeartbeatResponse>
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IRemoteCommandRepository _remoteCommandRepository;
    private readonly ISubscriptionService _subscriptionService;
    private readonly ITaskTimeEntryRepository _taskTimeEntryRepository;

    public HeartbeatCommandHandler(
        IDeviceRepository deviceRepository,
        IRemoteCommandRepository remoteCommandRepository,
        ISubscriptionService subscriptionService,
        ITaskTimeEntryRepository taskTimeEntryRepository)
    {
        _deviceRepository = deviceRepository;
        _remoteCommandRepository = remoteCommandRepository;
        _subscriptionService = subscriptionService;
        _taskTimeEntryRepository = taskTimeEntryRepository;
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

        // If tracking is not actively "running", pause any open task timer for this user.
        // This prevents a task entry (EndedAt=null) from being treated as "running until now"
        // in the WebUI when tracking is paused/stopped/idle.
        if (!string.IsNullOrWhiteSpace(request.TrackingState) &&
            !string.Equals(request.TrackingState, "running", StringComparison.OrdinalIgnoreCase))
        {
            var openEntries = await _taskTimeEntryRepository.ListOpenForUserAsync(device.UserId, cancellationToken);
            foreach (var open in openEntries)
            {
                if (open.IsPaused) continue;
                open.Pause();
                await _taskTimeEntryRepository.UpdateAsync(open, cancellationToken);
            }
        }

        // Check if there are pending commands for this device
        var pendingCommands = await _remoteCommandRepository.GetPendingByDeviceIdAsync(
            request.DeviceId, cancellationToken);

        var subscriptionCheck = await _subscriptionService.CheckSubscriptionAccessAsync(device.OrgId, cancellationToken);

        return new HeartbeatResponse
        {
            LastSeenAt = device.LastHeartbeatAt ?? DateTime.UtcNow,
            Status = device.Status.ToString(),
            HasPendingCommands = pendingCommands.Any(),
            SubscriptionStatus = subscriptionCheck.Status,
            GracePeriodEnd = subscriptionCheck.GracePeriodEnd
        };
    }
}
