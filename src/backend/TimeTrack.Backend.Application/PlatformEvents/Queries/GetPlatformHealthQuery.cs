using MediatR;
using TimeTrack.Backend.Application.PlatformEvents.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.PlatformEvents.Queries;

public sealed record GetPlatformHealthQuery : IRequest<PlatformHealthDto>;

public sealed class GetPlatformHealthQueryHandler
    : IRequestHandler<GetPlatformHealthQuery, PlatformHealthDto>
{
    private readonly IPlatformHealthStateRepository _repository;

    public GetPlatformHealthQueryHandler(IPlatformHealthStateRepository repository)
    {
        _repository = repository;
    }

    public async Task<PlatformHealthDto> Handle(GetPlatformHealthQuery request, CancellationToken cancellationToken)
    {
        var state = await _repository.GetAsync(cancellationToken);
        return new PlatformHealthDto
        {
            Status = state.Status,
            ChecksJson = state.ChecksJson,
            LastChangedAtUtc = state.LastChangedAtUtc,
            UpdatedAtUtc = state.UpdatedAtUtc
        };
    }
}

