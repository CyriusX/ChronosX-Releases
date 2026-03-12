using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Services;

namespace TimeTrack.Agent.Infrastructure.Services;

/// <summary>
/// Implementação de ITokenStore para modo local/teste (sem backend)
/// Armazena tokens em memória para permitir isolamento de usuário
/// </summary>
public sealed class NullTokenStore : ITokenStore
{
    private readonly ILogger<NullTokenStore> _logger;
    private string? _jwt;
    private string? _refreshToken;
    private readonly object _lock = new();

    public NullTokenStore(ILogger<NullTokenStore> logger)
    {
        _logger = logger;
    }

    public event EventHandler<TokensStoredEventArgs>? TokensStored;
    public event EventHandler? TokensCleared;

    public Task<string?> GetJwtAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_jwt);
        }
    }

    public Task<string?> GetRefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_refreshToken);
        }
    }

    public Task StoreTokensAsync(string jwt, string refreshToken, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _jwt = jwt;
            _refreshToken = refreshToken;
        }

        _logger.LogInformation("NullTokenStore: Tokens stored in memory");
        TokensStored?.Invoke(this, new TokensStoredEventArgs
        {
            Jwt = jwt,
            RefreshToken = refreshToken
        });
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _jwt = null;
            _refreshToken = null;
        }

        _logger.LogDebug("NullTokenStore: Tokens cleared");
        TokensCleared?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public bool IsJwtExpiringSoon(int withinMinutes = 5)
    {
        // Se não há token, considera como "expirando"
        if (_jwt == null) return true;

        // Tenta extrair exp do JWT
        try
        {
            var parts = _jwt.Split('.');
            if (parts.Length != 3) return true;

            var payload = parts[1];
            var padding = payload.Length % 4;
            if (padding > 0)
                payload += new string('=', 4 - padding);

            var jsonBytes = Convert.FromBase64String(payload);
            var json = System.Text.Encoding.UTF8.GetString(jsonBytes);
            using var doc = System.Text.Json.JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("exp", out var expElement))
            {
                var exp = expElement.GetInt64();
                var expiresAt = DateTimeOffset.FromUnixTimeSeconds(exp);
                return expiresAt.UtcDateTime <= DateTime.UtcNow.AddMinutes(withinMinutes);
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return false;
    }

    public Task<bool> RefreshAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("NullTokenStore: Token refresh attempted but no backend is configured");
        return Task.FromResult(false);
    }
}
