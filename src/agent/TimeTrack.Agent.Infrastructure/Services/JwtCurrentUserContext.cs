using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Infrastructure.Persistence;

namespace TimeTrack.Agent.Infrastructure.Services;

/// <summary>
/// Implementação de ICurrentUserContext que extrai informações do JWT
///
/// SOLID:
/// - SRP: Apenas gerencia contexto do usuário atual
/// - DIP: Depende de ITokenStore (abstração)
///
/// Composition:
/// - Usa ITokenStore para obter o JWT
/// - Usa SqliteContext para migrar registros órfãos
/// - Não herda de nenhuma classe base
/// </summary>
public sealed class JwtCurrentUserContext : ICurrentUserContext
{
    private readonly ITokenStore _tokenStore;
    private readonly SqliteContext _sqliteContext;
    private readonly ILogger<JwtCurrentUserContext> _logger;
    private readonly object _lock = new();

    private Guid? _cachedUserId;
    private Guid? _cachedOrgId;
    private bool _initialized;
    private HashSet<Guid> _migratedUsers = new();

    public event EventHandler<UserChangedEventArgs>? UserChanged;

    public JwtCurrentUserContext(
        ITokenStore tokenStore,
        SqliteContext sqliteContext,
        ILogger<JwtCurrentUserContext> logger)
    {
        _tokenStore = tokenStore;
        _sqliteContext = sqliteContext;
        _logger = logger;

        // Subscrever aos eventos do token store
        _tokenStore.TokensStored += async (sender, args) =>
        {
            _logger.LogDebug("Tokens stored event received, refreshing user context");
            await RefreshAsync();
        };

        _tokenStore.TokensCleared += async (sender, args) =>
        {
            _logger.LogDebug("Tokens cleared event received, clearing user context");
            await ClearAsync();
        };
    }

    public Guid? UserId
    {
        get
        {
            EnsureInitialized();
            return _cachedUserId;
        }
    }

    public Guid? OrgId
    {
        get
        {
            EnsureInitialized();
            return _cachedOrgId;
        }
    }

    public bool IsAuthenticated => UserId.HasValue;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        Guid? previousUserId;

        lock (_lock)
        {
            previousUserId = _cachedUserId;
        }

        var jwt = await _tokenStore.GetJwtAsync(cancellationToken);

        Guid? newUserId = null;
        Guid? newOrgId = null;

        if (!string.IsNullOrEmpty(jwt))
        {
            var claims = ExtractClaimsFromJwt(jwt);
            newUserId = claims.UserId;
            newOrgId = claims.OrgId;
        }

        lock (_lock)
        {
            _cachedUserId = newUserId;
            _cachedOrgId = newOrgId;
            _initialized = true;
        }

        // Migrar registros órfãos se é um novo usuário (não migrado ainda)
        if (newUserId.HasValue)
        {
            bool shouldMigrate;
            lock (_lock)
            {
                shouldMigrate = _migratedUsers.Add(newUserId.Value);
            }

            if (shouldMigrate)
            {
                try
                {
                    await _sqliteContext.MigrateOrphanRecordsToUserAsync(newUserId.Value, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to migrate orphan records for user {UserId}", newUserId.Value);
                }
            }
        }

        // Disparar evento se usuário mudou
        if (previousUserId != newUserId)
        {
            _logger.LogInformation(
                "User context changed: {PreviousUserId} -> {NewUserId}",
                previousUserId,
                newUserId);

            UserChanged?.Invoke(this, new UserChangedEventArgs
            {
                PreviousUserId = previousUserId,
                NewUserId = newUserId
            });
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        Guid? previousUserId;

        lock (_lock)
        {
            previousUserId = _cachedUserId;
            _cachedUserId = null;
            _cachedOrgId = null;
            _initialized = true;
        }

        if (previousUserId.HasValue)
        {
            _logger.LogInformation("User context cleared");

            UserChanged?.Invoke(this, new UserChangedEventArgs
            {
                PreviousUserId = previousUserId,
                NewUserId = null
            });
        }
    }

    private void EnsureInitialized()
    {
        lock (_lock)
        {
            if (!_initialized)
            {
                // Inicialização síncrona usando GetAwaiter().GetResult()
                // Isso é aceitável porque RefreshAsync é rápido e só lê do cache
                RefreshAsync().GetAwaiter().GetResult();
            }
        }
    }

    private static (Guid? UserId, Guid? OrgId) ExtractClaimsFromJwt(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length != 3)
                return (null, null);

            var payload = parts[1];
            var padding = payload.Length % 4;
            if (padding > 0)
                payload += new string('=', 4 - padding);

            var jsonBytes = Convert.FromBase64String(payload);
            var json = Encoding.UTF8.GetString(jsonBytes);
            var payloadObj = JsonSerializer.Deserialize<JsonElement>(json);

            Guid? userId = null;
            Guid? orgId = null;

            // Tentar diferentes claim names
            if (payloadObj.TryGetProperty("sub", out var subElement))
            {
                if (Guid.TryParse(subElement.GetString(), out var subGuid))
                    userId = subGuid;
            }
            else if (payloadObj.TryGetProperty("user_id", out var userIdElement))
            {
                if (Guid.TryParse(userIdElement.GetString(), out var userGuid))
                    userId = userGuid;
            }

            if (payloadObj.TryGetProperty("org_id", out var orgIdElement))
            {
                if (Guid.TryParse(orgIdElement.GetString(), out var orgGuid))
                    orgId = orgGuid;
            }

            return (userId, orgId);
        }
        catch
        {
            return (null, null);
        }
    }
}
