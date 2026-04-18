using System.Text.Json;
using MediatR;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Maintenance.DTOs;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Maintenance.Commands;

public sealed record CreateRemoteCommandCommand(
    Guid OrgId,
    Guid DeviceId,
    string CommandType,
    object? Payload) : IRequest<CreateRemoteCommandResponse>;

public sealed class CreateRemoteCommandCommandHandler
    : IRequestHandler<CreateRemoteCommandCommand, CreateRemoteCommandResponse>
{
    private static readonly HashSet<string> ValidCommandTypes = new()
    {
        "restart", "stop_tracking", "resume_tracking", "force_sync", "send_notification", "force_update", "set_devtools"
    };

    private readonly IRemoteCommandRepository _commandRepository;
    private readonly ICurrentUserContext _currentUser;

    public CreateRemoteCommandCommandHandler(
        IRemoteCommandRepository commandRepository,
        ICurrentUserContext currentUser)
    {
        _commandRepository = commandRepository;
        _currentUser = currentUser;
    }

    public async Task<CreateRemoteCommandResponse> Handle(
        CreateRemoteCommandCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.OrgId.HasValue || !_currentUser.UserId.HasValue)
            throw new UnauthorizedAccessException("User context not available");

        if (_currentUser.OrgId != request.OrgId)
            throw new UnauthorizedAccessException("Access denied to this organization");

        if (!ValidCommandTypes.Contains(request.CommandType))
            throw new ArgumentException($"Invalid command type: {request.CommandType}");

        var payloadJson = request.Payload != null
            ? JsonSerializer.Serialize(request.Payload)
            : null;

        var command = RemoteCommand.Create(
            request.OrgId,
            request.DeviceId,
            request.CommandType,
            payloadJson,
            _currentUser.UserId.Value,
            ttl: request.CommandType == "set_devtools" ? TimeSpan.FromDays(7) : null);

        await _commandRepository.AddAsync(command, cancellationToken);

        return new CreateRemoteCommandResponse
        {
            CommandId = command.Id,
            Status = "pending"
        };
    }
}
