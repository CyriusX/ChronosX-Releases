using MediatR;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Integrations.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Integrations.Linear.Queries;

// ═══════════════════════════════════════════════════════════════════════════
// LIST MY INTEGRATIONS
// ═══════════════════════════════════════════════════════════════════════════

public sealed record ListMyIntegrationsQuery : IRequest<ListUserIntegrationsResponse>;

public sealed class ListMyIntegrationsQueryHandler : IRequestHandler<ListMyIntegrationsQuery, ListUserIntegrationsResponse>
{
    private readonly IUserIntegrationRepository _integrations;
    private readonly ICurrentUserContext _currentUser;

    public ListMyIntegrationsQueryHandler(
        IUserIntegrationRepository integrations,
        ICurrentUserContext currentUser)
    {
        _integrations = integrations;
        _currentUser = currentUser;
    }

    public async Task<ListUserIntegrationsResponse> Handle(ListMyIntegrationsQuery request, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue) throw new UnauthorizedAccessException();

        var rows = await _integrations.ListByUserAsync(_currentUser.UserId.Value, ct);
        return new ListUserIntegrationsResponse
        {
            Integrations = rows.Select(IntegrationMapper.Map).ToList()
        };
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// LIST LINEAR SYNC HISTORY
// ═══════════════════════════════════════════════════════════════════════════

public sealed record ListLinearSyncHistoryQuery : IRequest<ListLinearSyncHistoryResponse>;

public sealed class ListLinearSyncHistoryQueryHandler : IRequestHandler<ListLinearSyncHistoryQuery, ListLinearSyncHistoryResponse>
{
    private readonly ILinearSyncHistoryRepository _history;
    private readonly ICurrentUserContext _currentUser;

    public ListLinearSyncHistoryQueryHandler(
        ILinearSyncHistoryRepository history,
        ICurrentUserContext currentUser)
    {
        _history = history;
        _currentUser = currentUser;
    }

    public async Task<ListLinearSyncHistoryResponse> Handle(ListLinearSyncHistoryQuery request, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue) throw new UnauthorizedAccessException();

        var rows = await _history.ListRecentForUserAsync(_currentUser.UserId.Value, take: 20, ct);
        return new ListLinearSyncHistoryResponse
        {
            Entries = rows.Select(IntegrationMapper.Map).ToList()
        };
    }
}
