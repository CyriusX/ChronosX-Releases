using MediatR;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Maintenance.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Maintenance.Queries;

public sealed record GetDeviceCommandsQuery(Guid OrgId, Guid DeviceId) : IRequest<CommandHistoryResponse?>;

public sealed class GetDeviceCommandsQueryHandler
    : IRequestHandler<GetDeviceCommandsQuery, CommandHistoryResponse?>
{
    private readonly IRemoteCommandRepository _commandRepository;
    private readonly ICurrentUserContext _currentUser;

    public GetDeviceCommandsQueryHandler(
        IRemoteCommandRepository commandRepository,
        ICurrentUserContext currentUser)
    {
        _commandRepository = commandRepository;
        _currentUser = currentUser;
    }

    public async Task<CommandHistoryResponse?> Handle(
        GetDeviceCommandsQuery request,
        CancellationToken cancellationToken)
    {
        // Platform admins can access any organization
        if (!_currentUser.IsPlatformAdmin && _currentUser.OrgId != request.OrgId)
            return null;

        var commands = await _commandRepository.GetByDeviceIdAsync(
            request.DeviceId, 20, cancellationToken);

        return new CommandHistoryResponse
        {
            Commands = commands.Select(c => new CommandHistoryItem
            {
                Id = c.Id,
                CommandType = c.CommandType,
                Status = c.IsExpired && c.Status == "pending" ? "expired" : c.Status,
                PayloadJson = c.PayloadJson,
                ResultJson = c.ResultJson,
                CreatedAt = c.CreatedAt,
                AcknowledgedAt = c.AcknowledgedAt
            }).ToList()
        };
    }
}
