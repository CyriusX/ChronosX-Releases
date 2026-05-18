using System.Text.Json;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Api.OpsMcp;

public sealed class McpPlatformApiKeyMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;

    public McpPlatformApiKeyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IPlatformApiKeyHasher hasher,
        IPlatformApiKeyRepository repository,
        McpPlatformRequestContext requestContext,
        McpPerKeyRateLimiter rateLimiter,
        ILogger<McpPlatformApiKeyMiddleware> logger)
    {
        if (!context.Request.Path.StartsWithSegments("/mcp"))
        {
            await _next(context);
            return;
        }

        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsPost(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }

        var token = context.Request.Headers["X-Platform-Api-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token))
        {
            await WriteUnauthorizedAsync(context, "Missing X-Platform-Api-Key header.");
            return;
        }

        string tokenHash;
        try
        {
            tokenHash = hasher.HashToken(token);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Invalid platform API key header format.");
            await WriteUnauthorizedAsync(context, "Invalid platform API key.");
            return;
        }

        var apiKey = await repository.GetByKeyHashAsync(tokenHash, context.RequestAborted);
        if (apiKey is null || apiKey.RevokedAtUtc.HasValue)
        {
            await WriteUnauthorizedAsync(context, "Invalid platform API key.");
            return;
        }

        requestContext.PlatformApiKeyId = apiKey.Id;
        requestContext.PlatformApiKeyLabel = apiKey.Label;

        var (allowed, retryAfter) = await rateLimiter.TryAcquireAsync(apiKey.Id, context.RequestAborted);
        if (!allowed)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            if (retryAfter.HasValue)
            {
                context.Response.Headers.RetryAfter = Math.Max(1, (int)retryAfter.Value.TotalSeconds).ToString();
            }
            await context.Response.WriteAsJsonAsync(new
            {
                error = "TooManyRequests",
                message = "Rate limit exceeded. Please try again later."
            }, JsonOptions, context.RequestAborted);
            return;
        }

        try
        {
            // Avoid updating on every single request. One update per minute per key is enough.
            var now = DateTime.UtcNow;
            if (!apiKey.LastUsedAtUtc.HasValue || (now - apiKey.LastUsedAtUtc.Value) > TimeSpan.FromMinutes(1))
            {
                apiKey.RecordUsed();
                await repository.UpdateAsync(apiKey, context.RequestAborted);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed updating platform API key last_used_at_utc.");
        }

        await _next(context);
    }

    private static async Task WriteUnauthorizedAsync(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            error = "Unauthorized",
            message
        }, JsonOptions));
    }
}

