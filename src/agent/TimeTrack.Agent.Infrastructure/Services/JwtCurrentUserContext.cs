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
    private Guid? _cachedDeviceId;
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

        // Inicializar de forma assíncrona no construtor
        // para evitar deadlock
        _ = RefreshAsync();

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
            // Retorna o valor em cache (pode ser null se não inicializado)
            return _cachedUserId;
        }
    }

    public Guid? OrgId
    {
        get
        {
            // Retorna o valor em cache (pode ser null se não inicializado)
            return _cachedOrgId;
        }
    }

    public Guid? DeviceId
    {
        get
        {
            return _cachedDeviceId;
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
        Guid? newDeviceId = null;

        if (!string.IsNullOrEmpty(jwt))
        {
            var claims = ExtractClaimsFromJwt(jwt);
            newUserId = claims.UserId;
            newOrgId = claims.OrgId;
            newDeviceId = claims.DeviceId;
        }

        lock (_lock)
        {
            _cachedUserId = newUserId;
            _cachedOrgId = newOrgId;
            _cachedDeviceId = newDeviceId;
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
            _cachedDeviceId = null;
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
        // A inicialização agora é feita no construtor
        // Não precisamos mais de inicialização síncrona aqui
        // Se ainda não foi inicializado, retornamos null
        if (!_initialized)
        {
            _logger.LogDebug("EnsureInitialized: Not yet initialized, returning null");
        }
    }

    private (Guid? UserId, Guid? OrgId, Guid? DeviceId) ExtractClaimsFromJwt(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length != 3)
            {
                _logger.LogWarning("JWT does not have 3 parts; cannot extract claims");
                return (null, null, null);
            }

            var payload = parts[1];
            var padding = payload.Length % 4;
            if (padding > 0)
                payload += new string('=', 4 - padding);

            // JWTs use base64url — replace URL-safe characters so FromBase64String works.
            payload = payload.Replace('-', '+').Replace('_', '/');

            var jsonBytes = Convert.FromBase64String(payload);
            var json = Encoding.UTF8.GetString(jsonBytes);
            var payloadObj = JsonSerializer.Deserialize<JsonElement>(json);

            var userId = TryReadGuidClaim(payloadObj, "sub")
                ?? TryReadGuidClaim(payloadObj, "user_id")
                ?? TryReadGuidClaim(payloadObj, "userId")
                ?? TryReadGuidClaim(payloadObj, "nameid")
                ?? TryReadGuidClaim(payloadObj, "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");

            var orgId = TryReadGuidClaim(payloadObj, "org_id")
                ?? TryReadGuidClaim(payloadObj, "orgId")
                ?? TryReadGuidClaim(payloadObj, "organization_id");

            var deviceId = TryReadGuidClaim(payloadObj, "device_id")
                ?? TryReadGuidClaim(payloadObj, "deviceId");

            if (userId == null)
            {
                var availableClaims = string.Join(", ", EnumerateClaimNames(payloadObj));
                _logger.LogWarning(
                    "JWT parsed but no userId claim resolved to a Guid. Available claims: [{Claims}]",
                    availableClaims);
            }

            return (userId, orgId, deviceId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse JWT claims");
            return (null, null, null);
        }
    }

    private static Guid? TryReadGuidClaim(JsonElement payload, string claimName)
    {
        if (!payload.TryGetProperty(claimName, out var element))
            return null;

        if (element.ValueKind != JsonValueKind.String)
            return null;

        var value = element.GetString();
        return Guid.TryParse(value, out var guid) ? guid : (Guid?)null;
    }

    private static IEnumerable<string> EnumerateClaimNames(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object)
            yield break;

        foreach (var prop in payload.EnumerateObject())
            yield return prop.Name;
    }
}
