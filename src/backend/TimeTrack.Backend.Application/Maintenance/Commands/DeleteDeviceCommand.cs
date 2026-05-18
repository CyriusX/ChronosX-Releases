using MediatR;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Maintenance.Commands;

public sealed record DeleteDeviceCommand(Guid OrgId, Guid DeviceId) : IRequest<Unit>;

public sealed class DeleteDeviceCommandHandler : IRequestHandler<DeleteDeviceCommand, Unit>
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly ICurrentUserContext _currentUser;

    public DeleteDeviceCommandHandler(
        IDeviceRepository deviceRepository,
        ICurrentUserContext currentUser)
    {
        _deviceRepository = deviceRepository;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(DeleteDeviceCommand request, CancellationToken cancellationToken)
    {
        // Platform admins can access any organization
        if (!_currentUser.IsPlatformAdmin && _currentUser.OrgId != request.OrgId)
            throw new ForbiddenException("Access denied to this organization");

        var device = await _deviceRepository.GetByIdUnfilteredAsync(request.DeviceId, cancellationToken);
        if (device is null)
            throw new NotFoundException("Device", request.DeviceId);

        if (device.OrgId != request.OrgId)
            throw new ForbiddenException("Device does not belong to this organization");

        // Soft-delete: mark Inactive rather than hard-deleting.
        // Hard-deleting cascades to activity_sessions and destroys the user's work history.
        device.Deactivate();
        await _deviceRepository.UpdateAsync(device, cancellationToken);
        return Unit.Value;
    }
}
