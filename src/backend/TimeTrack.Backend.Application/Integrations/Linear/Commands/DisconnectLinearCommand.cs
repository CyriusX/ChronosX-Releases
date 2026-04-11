using MediatR;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.Integrations.Linear.Commands;

public sealed record DisconnectLinearCommand : IRequest<Unit>;

public sealed class DisconnectLinearCommandHandler : IRequestHandler<DisconnectLinearCommand, Unit>
{
    private readonly IUserIntegrationRepository _integrations;
    private readonly ICurrentUserContext _currentUser;

    public DisconnectLinearCommandHandler(
        IUserIntegrationRepository integrations,
        ICurrentUserContext currentUser)
    {
        _integrations = integrations;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(DisconnectLinearCommand request, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue) throw new UnauthorizedAccessException();

        var integration = await _integrations.GetAsync(_currentUser.UserId.Value, UserIntegrationProvider.Linear, ct)
            ?? throw new NotFoundException("UserIntegration", "Linear");

        // Delete the row entirely — a disconnected integration carries no value.
        // Historical LinearSyncHistory rows remain for audit.
        await _integrations.DeleteAsync(integration, ct);
        return Unit.Value;
    }
}
