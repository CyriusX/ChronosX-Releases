using MediatR;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Integrations.DTOs;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.Integrations.Linear.Commands;

public sealed record ConnectLinearCommand(string ApiKey) : IRequest<UserIntegrationResponse>;

public sealed class ConnectLinearCommandHandler : IRequestHandler<ConnectLinearCommand, UserIntegrationResponse>
{
    private readonly IUserIntegrationRepository _integrations;
    private readonly ILinearClient _linear;
    private readonly IUserIntegrationTokenProtector _protector;
    private readonly ICurrentUserContext _currentUser;
    private readonly ILogger<ConnectLinearCommandHandler> _logger;

    public ConnectLinearCommandHandler(
        IUserIntegrationRepository integrations,
        ILinearClient linear,
        IUserIntegrationTokenProtector protector,
        ICurrentUserContext currentUser,
        ILogger<ConnectLinearCommandHandler> logger)
    {
        _integrations = integrations;
        _linear = linear;
        _protector = protector;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<UserIntegrationResponse> Handle(ConnectLinearCommand request, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue || !_currentUser.OrgId.HasValue)
            throw new UnauthorizedAccessException();

        var userId = _currentUser.UserId.Value;
        var orgId = _currentUser.OrgId.Value;

        if (string.IsNullOrWhiteSpace(request.ApiKey))
            throw new ValidationException("ApiKey", "API key is required");
        var apiKey = request.ApiKey.Trim();

        // Validate by hitting Linear for the viewer — if the key is bad this throws LinearUnauthorizedException.
        LinearViewer viewer;
        try
        {
            viewer = await _linear.GetViewerAsync(apiKey, ct);
        }
        catch (LinearUnauthorizedException)
        {
            throw new ValidationException("ApiKey", "Linear rejected this API key. Check that you pasted it correctly.");
        }
        catch (LinearApiException ex)
        {
            _logger.LogWarning(ex, "Linear viewer query failed during connect");
            throw new ValidationException("ApiKey", "Could not reach Linear. Please try again.");
        }

        var encrypted = _protector.Protect(apiKey);

        var existing = await _integrations.GetAsync(userId, UserIntegrationProvider.Linear, ct);
        UserIntegration integration;
        if (existing is null)
        {
            integration = UserIntegration.Create(
                orgId,
                userId,
                UserIntegrationProvider.Linear,
                viewer.Id,
                viewer.Name,
                viewer.Email,
                encrypted);
            await _integrations.AddAsync(integration, ct);
        }
        else
        {
            existing.ReplaceToken(encrypted, viewer.Id, viewer.Name, viewer.Email);
            await _integrations.UpdateAsync(existing, ct);
            integration = existing;
        }

        _logger.LogInformation("Linear connected for user {UserId} (viewer {ViewerId})", userId, viewer.Id);
        return IntegrationMapper.Map(integration);
    }
}
