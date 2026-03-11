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
    string? AgentVersion) : IRequest<HeartbeatResponse>;

public sealed class HeartbeatCommandHandler : IRequestHandler<HeartbeatCommand, HeartbeatResponse>
{
    private readonly IDeviceRepository _deviceRepository;

    public HeartbeatCommandHandler(IDeviceRepository deviceRepository)
    {
        _deviceRepository = deviceRepository;
    }

    public async Task<HeartbeatResponse> Handle(HeartbeatCommand request, CancellationToken cancellationToken)
    {
        var device = await _deviceRepository.GetByIdAsync(request.DeviceId, cancellationToken);

        if (device is null)
        {
            throw new NotFoundException("Device", request.DeviceId);
        }

        device.RecordHeartbeat(request.AgentVersion);
        await _deviceRepository.UpdateAsync(device, cancellationToken);

        return new HeartbeatResponse
        {
            LastSeenAt = device.LastHeartbeatAt ?? DateTime.UtcNow,
            Status = device.Status.ToString()
        };
    }
}
