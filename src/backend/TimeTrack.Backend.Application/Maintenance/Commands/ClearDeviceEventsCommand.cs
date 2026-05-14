using MediatR;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Maintenance.Commands;

/// <summary>
/// Clears agent event logs for a specific device or all devices in the org.
/// </summary>
public sealed record ClearDeviceEventsCommand(
    Guid OrgId,
    Guid? DeviceId) : IRequest<int>;

public sealed class ClearDeviceEventsCommandHandler
    : IRequestHandler<ClearDeviceEventsCommand, int>
{
    private readonly IAgentEventLogRepository _eventLogRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly ICurrentUserContext _currentUser;

    public ClearDeviceEventsCommandHandler(
        IAgentEventLogRepository eventLogRepository,
        IDeviceRepository deviceRepository,
        ICurrentUserContext currentUser)
    {
        _eventLogRepository = eventLogRepository;
        _deviceRepository = deviceRepository;
        _currentUser = currentUser;
    }

    public async Task<int> Handle(ClearDeviceEventsCommand request, CancellationToken cancellationToken)
    {
        // Platform admins can access any organization
        if (!_currentUser.IsPlatformAdmin && _currentUser.OrgId != request.OrgId)
            throw new ForbiddenException("Access denied");

        if (request.DeviceId.HasValue)
        {
            // Verify device belongs to this org
            var device = await _deviceRepository.GetByIdAsync(request.DeviceId.Value, cancellationToken);
            if (device is null || device.OrgId != request.OrgId)
                throw new NotFoundException("Device", request.DeviceId.Value);

            return await _eventLogRepository.DeleteByDeviceIdAsync(request.DeviceId.Value, cancellationToken);
        }

        return await _eventLogRepository.DeleteByOrgIdAsync(request.OrgId, cancellationToken);
    }
}
