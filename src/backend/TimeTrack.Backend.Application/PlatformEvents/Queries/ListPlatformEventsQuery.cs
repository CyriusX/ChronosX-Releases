using MediatR;
using TimeTrack.Backend.Application.PlatformEvents.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.PlatformEvents.Queries;

public sealed record ListPlatformEventsQuery(
    DateTime? SinceUtc,
    string? Severity,
    int Limit) : IRequest<IReadOnlyList<PlatformEventLogDto>>;

public sealed class ListPlatformEventsQueryHandler
    : IRequestHandler<ListPlatformEventsQuery, IReadOnlyList<PlatformEventLogDto>>
{
    private readonly IPlatformEventLogRepository _repository;

    public ListPlatformEventsQueryHandler(IPlatformEventLogRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<PlatformEventLogDto>> Handle(
        ListPlatformEventsQuery request,
        CancellationToken cancellationToken)
    {
        var items = await _repository.ListAsync(
            request.SinceUtc,
            request.Severity,
            request.Limit,
            cancellationToken);

        return items.Select(e => new PlatformEventLogDto
        {
            Id = e.Id,
            EventType = e.EventType,
            Severity = e.Severity,
            Message = e.Message,
            MetadataJson = e.MetadataJson,
            TimestampUtc = e.TimestampUtc
        }).ToList();
    }
}

