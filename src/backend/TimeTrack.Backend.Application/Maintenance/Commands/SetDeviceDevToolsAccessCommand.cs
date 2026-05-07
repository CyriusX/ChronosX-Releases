using System.Text.Json;
using MediatR;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Maintenance.DTOs;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Maintenance.Commands;

public sealed record SetDeviceDevToolsAccessCommand(
    Guid OrgId,
    Guid DeviceId,
    bool Enabled) : IRequest<SetDevToolsAccessResponse>;

public sealed class SetDeviceDevToolsAccessCommandHandler
    : IRequestHandler<SetDeviceDevToolsAccessCommand, SetDevToolsAccessResponse>
{
    private static readonly TimeSpan EnabledDuration = TimeSpan.FromHours(24);
    private static readonly TimeSpan RemoteCommandTtl = TimeSpan.FromDays(7);

    private readonly IDeviceRepository _devices;
    private readonly IUserRepository _users;
    private readonly IRemoteCommandRepository _commands;
    private readonly ICurrentUserContext _currentUser;

    public SetDeviceDevToolsAccessCommandHandler(
        IDeviceRepository devices,
        IUserRepository users,
        IRemoteCommandRepository commands,
        ICurrentUserContext currentUser)
    {
        _devices = devices;
        _users = users;
        _commands = commands;
        _currentUser = currentUser;
    }

    public async Task<SetDevToolsAccessResponse> Handle(
        SetDeviceDevToolsAccessCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.OrgId.HasValue || !_currentUser.UserId.HasValue)
            throw new UnauthorizedAccessException("User context not available");

        if (_currentUser.OrgId != request.OrgId)
            throw new ForbiddenException("Access denied to this organization");

        var device = await _devices.GetByIdAsync(request.DeviceId, cancellationToken);
        if (device is null || device.OrgId != request.OrgId)
            throw new NotFoundException("Device", request.DeviceId);

        var user = device.User ?? await _users.GetByIdAsync(device.UserId, cancellationToken);
        if (user is null || user.OrgId != request.OrgId)
            throw new NotFoundException("User", device.UserId);

        var now = DateTime.UtcNow;
        var expiresAtUtc = request.Enabled ? now.Add(EnabledDuration) : (DateTime?)null;

        user.SetDevToolsEnabledUntilUtc(expiresAtUtc);
        await _users.UpdateAsync(user, cancellationToken);

        // Queue to the selected device always (even if it is currently inactive/offline),
        // plus any other active devices for the same user.
        var activeDevices = (await _devices.GetActiveByUserIdAsync(user.Id, cancellationToken)).ToList();
        var devicesToQueue = new Dictionary<Guid, Device>
        {
            [device.Id] = device
        };
        foreach (var d in activeDevices)
        {
            devicesToQueue[d.Id] = d;
        }

        var payloadJson = JsonSerializer.Serialize(new
        {
            enabled = request.Enabled,
            expiresAtUtc = expiresAtUtc?.ToString("O")
        });

        foreach (var d in devicesToQueue.Values)
        {
            var cmd = RemoteCommand.Create(
                request.OrgId,
                d.Id,
                commandType: "set_devtools",
                payloadJson: payloadJson,
                createdByUserId: _currentUser.UserId.Value,
                ttl: RemoteCommandTtl);

            await _commands.AddAsync(cmd, cancellationToken);
        }

        return new SetDevToolsAccessResponse
        {
            UserId = user.Id,
            Enabled = request.Enabled,
            ExpiresAtUtc = expiresAtUtc,
            QueuedDeviceCount = devicesToQueue.Count
        };
    }
}
