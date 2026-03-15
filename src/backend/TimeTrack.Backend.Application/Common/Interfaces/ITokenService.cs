namespace TimeTrack.Backend.Application.Common.Interfaces;

/// <summary>
/// Interface para geração e validação de tokens JWT
/// </summary>
public interface ITokenService
{
    string GenerateAccessToken(Guid userId, Guid orgId, string role, bool mustChangePassword = false);
    string GenerateAccessToken(Guid userId, Guid orgId, Guid? deviceId, string role, bool mustChangePassword = false);
    string GenerateRefreshToken();
    string HashRefreshToken(string token);
    bool ValidateRefreshToken(string token, string hashedToken);
    TimeSpan GetAccessTokenExpiration();
    TimeSpan GetRefreshTokenExpiration();
}
