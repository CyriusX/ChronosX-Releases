using MediatR;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Maintenance.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Maintenance.Queries;

public sealed record GetDeviceEventsQuery(
    Guid OrgId,
    Guid DeviceId,
    string? Category = null,
    string? Severity = null,
    DateTime? Since = null,
    int Limit = 100) : IRequest<DeviceEventsResponse?>;

public sealed class GetDeviceEventsQueryHandler : IRequestHandler<GetDeviceEventsQuery, DeviceEventsResponse?>
{
    private readonly IAgentEventLogRepository _eventLogRepository;
    private readonly ICurrentUserContext _currentUser;

    public GetDeviceEventsQueryHandler(
        IAgentEventLogRepository eventLogRepository,
        ICurrentUserContext currentUser)
    {
        _eventLogRepository = eventLogRepository;
        _currentUser = currentUser;
    }

    public async Task<DeviceEventsResponse?> Handle(
        GetDeviceEventsQuery request,
        CancellationToken cancellationToken)
    {
        // Platform admins can access any organization
        if (!_currentUser.IsPlatformAdmin && _currentUser.OrgId != request.OrgId)
            return null;

        var events = await _eventLogRepository.GetByDeviceIdAsync(
            request.DeviceId,
            request.Category,
            request.Severity,
            request.Since,
            request.Limit,
            cancellationToken);

        var items = events.Select(e => new DeviceEventItem
        {
            Id = e.Id,
            EventType = e.EventType,
            Category = e.Category,
            Severity = e.Severity,
            Message = e.Message,
            MetadataJson = e.MetadataJson,
            Timestamp = e.TimestampUtc
        }).ToList();

        return new DeviceEventsResponse
        {
            Events = items,
            TotalCount = items.Count
        };
    }
}
