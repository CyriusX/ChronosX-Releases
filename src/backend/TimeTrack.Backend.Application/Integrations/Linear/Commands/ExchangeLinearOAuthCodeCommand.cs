using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Integrations.DTOs;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.Integrations.Linear.Commands;

public sealed record ExchangeLinearOAuthCodeCommand(
    string Code,
    string State) : IRequest<UserIntegrationResponse>;

public sealed class ExchangeLinearOAuthCodeCommandHandler : IRequestHandler<ExchangeLinearOAuthCodeCommand, UserIntegrationResponse>
{
    private readonly ILinearClient _linearClient;
    private readonly IUserIntegrationRepository _userIntegrationRepository;
    private readonly IUserIntegrationTokenProtector _tokenProtector;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ExchangeLinearOAuthCodeCommandHandler> _logger;

    public ExchangeLinearOAuthCodeCommandHandler(
        ILinearClient linearClient,
        IUserIntegrationRepository userIntegrationRepository,
        IUserIntegrationTokenProtector tokenProtector,
        IConfiguration configuration,
        ILogger<ExchangeLinearOAuthCodeCommandHandler> logger)
    {
        _linearClient = linearClient;
        _userIntegrationRepository = userIntegrationRepository;
        _tokenProtector = tokenProtector;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<UserIntegrationResponse> Handle(ExchangeLinearOAuthCodeCommand request, CancellationToken cancellationToken)
    {
        var (userId, orgId) = DecodeState(request.State);

        var redirectUri = _configuration["LinearOAuth:RedirectUri"]
            ?? Environment.GetEnvironmentVariable("LINEAR_REDIRECT_URI")
            ?? "http://localhost:5000/api/v1/me/integrations/linear/oauth-callback";

        LinearOAuthTokenResponse tokenResponse;
        try
        {
            tokenResponse = await _linearClient.ExchangeCodeForTokenAsync(request.Code, redirectUri, cancellationToken);
        }
        catch (LinearApiException ex)
        {
            throw new ValidationException("Code", "Failed to exchange authorization code: " + ex.Message);
        }

        var accessToken = tokenResponse.AccessToken;
        var refreshToken = tokenResponse.RefreshToken;
        var expiresAt = tokenResponse.ExpiresIn > 0
            ? DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn)
            : (DateTime?)null;

        var encryptedAccessToken = _tokenProtector.Protect(accessToken);
        var encryptedRefreshToken = !string.IsNullOrEmpty(refreshToken)
            ? _tokenProtector.Protect(refreshToken)
            : null;

        string externalUserId = string.Empty;
        string? externalUserName = null;
        string? externalUserEmail = null;

        try
        {
            var viewer = await _linearClient.GetViewerAsync(accessToken, cancellationToken);
            externalUserId = viewer.Id;
            externalUserName = viewer.Name;
            externalUserEmail = viewer.Email;
        }
        catch (LinearApiException ex)
        {
            _logger.LogWarning(ex, "Failed to fetch Linear viewer info during OAuth callback");
        }

        var existingIntegration = await _userIntegrationRepository.GetAsync(
            userId,
            UserIntegrationProvider.Linear,
            cancellationToken);

        UserIntegration integration;

        if (existingIntegration != null)
        {
            existingIntegration.ReplaceToken(
                encryptedAccessToken,
                externalUserId,
                externalUserName,
                externalUserEmail,
                encryptedRefreshToken,
                expiresAt,
                UserIntegrationAuthMethod.OAuth);
            await _userIntegrationRepository.UpdateAsync(existingIntegration, cancellationToken);
            integration = existingIntegration;
        }
        else
        {
            integration = UserIntegration.Create(
                orgId,
                userId,
                UserIntegrationProvider.Linear,
                externalUserId,
                externalUserName,
                externalUserEmail,
                encryptedAccessToken,
                authMethod: UserIntegrationAuthMethod.OAuth,
                refreshToken: encryptedRefreshToken,
                tokenExpiresAt: expiresAt);
            await _userIntegrationRepository.AddAsync(integration, cancellationToken);
        }

        _logger.LogInformation("Linear OAuth connected for user {UserId}", userId);
        return IntegrationMapper.Map(integration);
    }

    private static (Guid userId, Guid orgId) DecodeState(string state)
    {
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(state));
            var doc = JsonDocument.Parse(json);
            var uid = doc.RootElement.GetProperty("uid").GetString()
                ?? throw new ValidationException("State", "Invalid state: missing user id");
            var oid = doc.RootElement.GetProperty("oid").GetString()
                ?? throw new ValidationException("State", "Invalid state: missing org id");
            return (Guid.Parse(uid), Guid.Parse(oid));
        }
        catch (FormatException)
        {
            throw new ValidationException("State", "Invalid state token format");
        }
        catch (JsonException)
        {
            throw new ValidationException("State", "Invalid state token payload");
        }
    }
}
