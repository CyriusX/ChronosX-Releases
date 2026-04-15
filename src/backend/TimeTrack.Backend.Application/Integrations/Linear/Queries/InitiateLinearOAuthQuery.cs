using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Configuration;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Integrations.DTOs;

namespace TimeTrack.Backend.Application.Integrations.Linear.Queries;

public sealed record InitiateLinearOAuthQuery : IRequest<InitiateLinearOAuthResponse>;

public sealed class InitiateLinearOAuthQueryHandler : IRequestHandler<InitiateLinearOAuthQuery, InitiateLinearOAuthResponse>
{
    private readonly ICurrentUserContext _currentUser;
    private readonly IConfiguration _configuration;

    public InitiateLinearOAuthQueryHandler(
        ICurrentUserContext currentUser,
        IConfiguration configuration)
    {
        _currentUser = currentUser;
        _configuration = configuration;
    }

    public async Task<InitiateLinearOAuthResponse> Handle(InitiateLinearOAuthQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.OrgId.HasValue || !_currentUser.UserId.HasValue)
            throw new UnauthorizedAccessException("User not authenticated");

        var statePayload = new
        {
            uid = _currentUser.UserId.Value.ToString(),
            oid = _currentUser.OrgId.Value.ToString(),
            rnd = GenerateRandomHex(16)
        };
        var stateJson = JsonSerializer.Serialize(statePayload);
        var state = Convert.ToBase64String(Encoding.UTF8.GetBytes(stateJson));

        var clientId = _configuration["LinearOAuth:ClientId"] is { Length: > 0 } cid ? cid
            : Environment.GetEnvironmentVariable("LINEAR_CLIENT_ID") ?? "";
        var clientSecret = _configuration["LinearOAuth:ClientSecret"] is { Length: > 0 } cs ? cs
            : Environment.GetEnvironmentVariable("LINEAR_CLIENT_SECRET") ?? "";
        var redirectUri = _configuration["LinearOAuth:RedirectUri"] is { Length: > 0 } ri ? ri
            : Environment.GetEnvironmentVariable("LINEAR_REDIRECT_URI")
            ?? "http://localhost:5000/api/v1/me/integrations/linear/oauth-callback";
        var scope = _configuration["LinearOAuth:Scope"] is { Length: > 0 } s ? s : "read,write";

        var authorizeUrl = $"https://linear.app/oauth/authorize" +
            $"?client_id={Uri.EscapeDataString(clientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&response_type=code" +
            $"&scope={Uri.EscapeDataString(scope)}" +
            $"&state={Uri.EscapeDataString(state)}";

        return new InitiateLinearOAuthResponse
        {
            AuthorizeUrl = authorizeUrl,
            State = state
        };
    }

    private static string GenerateRandomHex(int byteCount)
    {
        var bytes = new byte[byteCount];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToHexString(bytes);
    }
}
