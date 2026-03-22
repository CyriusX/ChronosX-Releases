using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace TimeTrack.Api.Extensions;

/// <summary>
/// Extension methods for configuring rate limiting
/// </summary>
public static class RateLimitingExtensions
{
    /// <summary>
    /// Policy names for rate limiting
    /// </summary>
    public static class PolicyNames
    {
        /// <summary>
        /// Strict rate limit for authentication endpoints (5 req / 15 min / by IP)
        /// </summary>
        public const string Auth = "AuthPolicy";

        /// <summary>
        /// Rate limit for ingest endpoints (100 req / 1 min / by device_id)
        /// </summary>
        public const string Ingest = "IngestPolicy";

        /// <summary>
        /// Rate limit for report endpoints (30 req / 1 min / by user_id)
        /// </summary>
        public const string Reports = "ReportsPolicy";

        /// <summary>
        /// Default rate limit for all other endpoints (200 req / 1 min / by user_id or IP)
        /// </summary>
        public const string Default = "DefaultPolicy";
    }

    /// <summary>
    /// Configures rate limiting policies for the API
    /// </summary>
    public static IServiceCollection AddRateLimitingPolicies(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = (int)HttpStatusCode.TooManyRequests;

            // Add retry-after header on rate limit exceeded
            options.OnRejected = async (context, cancellationToken) =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<RateLimiter>>();

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = retryAfter.TotalSeconds.ToString();
                    logger.LogWarning(
                        "Rate limit exceeded for {Path}. Retry after {RetryAfter}s",
                        context.HttpContext.Request.Path,
                        retryAfter.TotalSeconds);
                }
                else
                {
                    // Default retry after 60 seconds if no metadata available
                    context.HttpContext.Response.Headers.RetryAfter = "60";
                    logger.LogWarning(
                        "Rate limit exceeded for {Path}",
                        context.HttpContext.Request.Path);
                }

                context.HttpContext.Response.ContentType = "application/json";

                var response = new
                {
                    error = "TooManyRequests",
                    message = "Rate limit exceeded. Please try again later."
                };

                await context.HttpContext.Response.WriteAsJsonAsync(response, cancellationToken);
            };

            // Auth policy: 5 requests per 15 minutes by IP
            options.AddFixedWindowLimiter(PolicyNames.Auth, opt =>
            {
                opt.PermitLimit = 5;
                opt.Window = TimeSpan.FromMinutes(15);
                opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opt.QueueLimit = 0;
            });

            // Ingest policy: 100 requests per 1 minute by device_id
            options.AddFixedWindowLimiter(PolicyNames.Ingest, opt =>
            {
                opt.PermitLimit = 100;
                opt.Window = TimeSpan.FromMinutes(1);
                opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opt.QueueLimit = 0;
            });

            // Reports policy: 60 requests per 1 minute by user_id
            // (Reports page makes 6 parallel requests, allow for multiple page refreshes)
            options.AddFixedWindowLimiter(PolicyNames.Reports, opt =>
            {
                opt.PermitLimit = 60;
                opt.Window = TimeSpan.FromMinutes(1);
                opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opt.QueueLimit = 0;
            });

            // Default policy: 200 requests per 1 minute by user_id or IP
            options.AddFixedWindowLimiter(PolicyNames.Default, opt =>
            {
                opt.PermitLimit = 200;
                opt.Window = TimeSpan.FromMinutes(1);
                opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                opt.QueueLimit = 0;
            });
        });

        return services;
    }

    /// <summary>
    /// Gets the client IP address from the HttpContext, respecting X-Forwarded-For header
    /// </summary>
    public static string GetClientIpAddress(this HttpContext context)
    {
        // Check for forwarded header first (when behind proxy/load balancer)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            // Take the first IP in the chain (original client)
            var ip = forwardedFor.Split(',').FirstOrDefault()?.Trim();
            if (!string.IsNullOrEmpty(ip))
            {
                return ip;
            }
        }

        // Fallback to direct connection IP
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    /// <summary>
    /// Gets the partition key for rate limiting based on device_id from JWT claims
    /// </summary>
    public static string GetDevicePartitionKey(this HttpContext context)
    {
        // Extract device_id from JWT claims
        var deviceId = context.User.FindFirst("device_id")?.Value;

        // If no device_id, fall back to user_id (for web clients)
        return deviceId
            ?? context.User.FindFirst("sub")?.Value
            ?? context.GetClientIpAddress();
    }

    /// <summary>
    /// Gets the partition key for rate limiting based on user_id from JWT claims
    /// </summary>
    public static string GetUserPartitionKey(this HttpContext context)
    {
        // Extract user_id from JWT claims
        return context.User.FindFirst("sub")?.Value
            ?? context.GetClientIpAddress();
    }
}
