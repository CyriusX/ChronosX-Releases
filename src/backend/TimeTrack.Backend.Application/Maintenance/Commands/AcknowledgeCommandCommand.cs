using MediatR;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Maintenance.Commands;

public sealed record AcknowledgeCommandCommand(
    Guid CommandId,
    string Status,
    string? ResultJson) : IRequest;

public sealed class AcknowledgeCommandCommandHandler : IRequestHandler<AcknowledgeCommandCommand>
{
    private readonly IRemoteCommandRepository _commandRepository;

    public AcknowledgeCommandCommandHandler(IRemoteCommandRepository commandRepository)
    {
        _commandRepository = commandRepository;
    }

    public async Task Handle(AcknowledgeCommandCommand request, CancellationToken cancellationToken)
    {
        var command = await _commandRepository.GetByIdAsync(request.CommandId, cancellationToken);

        if (command is null)
            throw new NotFoundException("RemoteCommand", request.CommandId);

        if (request.Status == "completed")
            command.MarkCompleted(request.ResultJson);
        else
            command.MarkFailed(request.ResultJson ?? "Unknown error");

        await _commandRepository.UpdateAsync(command, cancellationToken);
    }
}
