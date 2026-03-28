using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Infrastructure.Services;

/// <summary>
/// Implementação do serviço de auditoria
///
/// IMPORTANT: This service uses fire-and-forget pattern for audit logging.
/// To avoid DbContext concurrency issues, background operations create their own
/// service scope with separate DbContext instances.
/// </summary>
public sealed class AuditLogService : IAuditLogService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICurrentUserContext _currentUser;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public AuditLogService(
        IServiceScopeFactory scopeFactory,
        ICurrentUserContext currentUser,
        IHttpContextAccessor httpContextAccessor)
    {
        _scopeFactory = scopeFactory;
        _currentUser = currentUser;
        _httpContextAccessor = httpContextAccessor;
    }

    public void LogAsync(
        string action,
        string entityType,
        Guid? entityId = null,
        object? metadata = null,
        CancellationToken cancellationToken = default)
    {
        // Capture context from current scope BEFORE starting background task
        // This is critical because HttpContext and ICurrentUserContext are scoped
        var orgId = _currentUser.OrgId;
        var userId = _currentUser.UserId;
        var httpContext = _httpContextAccessor.HttpContext;
        var ipAddress = httpContext?.Connection?.RemoteIpAddress?.ToString();
        var userAgent = httpContext?.Request?.Headers["User-Agent"].ToString();

        // Fire-and-forget pattern with its own scope to avoid DbContext concurrency issues
        _ = Task.Run(async () =>
        {
            try
            {
                // Create a new scope for this background operation
                // This ensures we get a fresh DbContext, avoiding concurrent access
                using var scope = _scopeFactory.CreateScope();
                var auditLogRepository = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();

                await LogWithCapturedContextAsync(
                    auditLogRepository,
                    orgId,
                    userId,
                    action,
                    entityType,
                    entityId,
                    metadata,
                    ipAddress,
                    userAgent,
                    cancellationToken);
            }
            catch
            {
                // Silently fail - audit logging should not break the application
            }
        }, cancellationToken);
    }

    public async Task LogAsync(
        string action,
        string entityType,
        Guid? entityId,
        object? metadata,
        bool waitForCompletion,
        CancellationToken cancellationToken = default)
    {
        if (waitForCompletion)
        {
            await LogWithHttpContextAsync(action, entityType, entityId, metadata, null, null, cancellationToken);
        }
        else
        {
            LogAsync(action, entityType, entityId, metadata, cancellationToken);
        }
    }

    public async Task LogWithHttpContextAsync(
        string action,
        string entityType,
        Guid? entityId = null,
        object? metadata = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        var orgId = _currentUser.OrgId;
        var userId = _currentUser.UserId;

        // Skip if no organization context (system operations)
        if (!orgId.HasValue)
        {
            return;
        }

        await LogExplicitAsync(
            orgId.Value,
            userId,
            action,
            entityType,
            entityId,
            metadata,
            ipAddress,
            userAgent,
            cancellationToken);
    }

    public async Task LogExplicitAsync(
        Guid orgId,
        Guid? userId,
        string action,
        string entityType,
        Guid? entityId = null,
        object? metadata = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        // Capture HTTP context info if not provided
        var httpContext = _httpContextAccessor.HttpContext;
        var capturedIpAddress = ipAddress ?? httpContext?.Connection?.RemoteIpAddress?.ToString();
        var capturedUserAgent = userAgent ?? httpContext?.Request?.Headers["User-Agent"].ToString();

        // Sanitize metadata to remove sensitive data
        var sanitizedMetadata = SanitizeMetadata(metadata);

        var auditLog = AuditLog.Create(
            orgId,
            userId,
            action,
            entityType,
            entityId,
            oldValues: null,
            newValues: sanitizedMetadata,
            ipAddress: capturedIpAddress,
            userAgent: capturedUserAgent
        );

        // For explicit logging (not fire-and-forget), we need to create a scope
        // to get the repository since we might be called from a background thread
        using var scope = _scopeFactory.CreateScope();
        var auditLogRepository = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();
        await auditLogRepository.AddAsync(auditLog, cancellationToken);
    }

    /// <summary>
    /// Internal method for fire-and-forget logging with captured context
    /// </summary>
    private static async Task LogWithCapturedContextAsync(
        IAuditLogRepository auditLogRepository,
        Guid? orgId,
        Guid? userId,
        string action,
        string entityType,
        Guid? entityId,
        object? metadata,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        // Skip if no organization context (system operations)
        if (!orgId.HasValue)
        {
            return;
        }

        var sanitizedMetadata = SanitizeMetadata(metadata);

        var auditLog = AuditLog.Create(
            orgId.Value,
            userId,
            action,
            entityType,
            entityId,
            oldValues: null,
            newValues: sanitizedMetadata,
            ipAddress: ipAddress,
            userAgent: userAgent
        );

        await auditLogRepository.AddAsync(auditLog, cancellationToken);
    }

    /// <summary>
    /// Sanitizes metadata to remove sensitive information
    /// </summary>
    private static string? SanitizeMetadata(object? metadata)
    {
        if (metadata == null)
        {
            return null;
        }

        // List of sensitive field names that should be redacted
        var sensitiveFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "password",
            "passwordHash",
            "currentPassword",
            "newPassword",
            "confirmPassword",
            "token",
            "accessToken",
            "refreshToken",
            "secret",
            "apiKey",
            "authorization",
            "cookie"
        };

        try
        {
            var json = JsonSerializer.Serialize(metadata, JsonOptions);
            var dictionary = JsonSerializer.Deserialize<Dictionary<string, object?>>(json, JsonOptions);

            if (dictionary == null)
            {
                return json;
            }

            var sanitized = new Dictionary<string, object?>();
            foreach (var kvp in dictionary)
            {
                if (sensitiveFields.Contains(kvp.Key))
                {
                    sanitized[kvp.Key] = "[REDACTED]";
                }
                else
                {
                    sanitized[kvp.Key] = kvp.Value;
                }
            }

            return JsonSerializer.Serialize(sanitized, JsonOptions);
        }
        catch
        {
            // If serialization fails, return a safe representation
            return "{\"error\":\"Unable to serialize metadata\"}";
        }
    }
}
