using MediatR;
using TimeTrack.Backend.Application.Maintenance.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Maintenance.Queries;

public sealed record GetPendingCommandsQuery(Guid DeviceId) : IRequest<PendingCommandsResponse>;

public sealed class GetPendingCommandsQueryHandler
    : IRequestHandler<GetPendingCommandsQuery, PendingCommandsResponse>
{
    private readonly IRemoteCommandRepository _commandRepository;

    public GetPendingCommandsQueryHandler(IRemoteCommandRepository commandRepository)
    {
        _commandRepository = commandRepository;
    }

    public async Task<PendingCommandsResponse> Handle(
        GetPendingCommandsQuery request,
        CancellationToken cancellationToken)
    {
        var commands = await _commandRepository.GetPendingByDeviceIdAsync(
            request.DeviceId, cancellationToken);

        return new PendingCommandsResponse
        {
            Commands = commands.Select(c => new PendingCommandItem
            {
                Id = c.Id,
                CommandType = c.CommandType,
                PayloadJson = c.PayloadJson,
                CreatedAt = c.CreatedAt
            }).ToList()
        };
    }
}
