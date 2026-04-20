using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Api.Middleware;

public sealed class SubscriptionCheckMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SubscriptionCheckMiddleware> _logger;

    public SubscriptionCheckMiddleware(RequestDelegate next, IMemoryCache cache, ILogger<SubscriptionCheckMiddleware> logger)
    {
        _next = next;
        _cache = cache;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentUserContext currentUser, IOrgSubscriptionRepository subscriptionRepository)
    {
        var endpoint = context.GetEndpoint();
        var requireSub = endpoint?.Metadata.GetMetadata<RequireSubscriptionAttribute>();

        if (requireSub is null)
        {
            await _next(context);
            return;
        }

        if (!currentUser.IsAuthenticated || !currentUser.OrgId.HasValue)
        {
            await _next(context);
            return;
        }

        var orgId = currentUser.OrgId.Value;
        var cacheKey = $"sub_check_{orgId}";
        bool hasAccess;
        bool isInGracePeriod;
        Dictionary<string, bool>? features = null;

        if (!_cache.TryGetValue(cacheKey, out (bool HasAccess, bool IsInGracePeriod, Dictionary<string, bool>? Features) cached))
        {
            var subscription = await subscriptionRepository.GetByOrgIdUnfilteredAsync(orgId, context.RequestAborted);
            hasAccess = subscription?.HasActiveAccess() ?? false;
            isInGracePeriod = subscription?.IsInGracePeriod() ?? false;
            features = subscription?.Plan?.ToFeatureDictionary();

            _cache.Set(cacheKey, (hasAccess, isInGracePeriod, features), TimeSpan.FromSeconds(60));
        }
        else
        {
            hasAccess = cached.HasAccess;
            isInGracePeriod = cached.IsInGracePeriod;
            features = cached.Features;
        }

        if (!hasAccess)
        {
            context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                code = "subscription_required",
                message = "Active subscription required to access this resource"
            });
            return;
        }

        if (!string.IsNullOrEmpty(requireSub.RequiredFeature))
        {
            if (!SubscriptionPlan.IsValidFeatureName(requireSub.RequiredFeature))
            {
                _logger.LogWarning("Unknown feature name on [RequireSubscription]: {Feature}", requireSub.RequiredFeature);
            }

            var featureEnabled = features?.TryGetValue(requireSub.RequiredFeature, out var enabled) == true && enabled;

            if (!featureEnabled)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    code = "feature_not_enabled",
                    message = $"Feature '{requireSub.RequiredFeature}' is not available in your current plan"
                });
                return;
            }
        }

        if (isInGracePeriod)
        {
            context.Response.Headers.Append("X-Subscription-Warning", "grace-period");
        }

        await _next(context);
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireSubscriptionAttribute : Attribute
{
    public string? RequiredFeature { get; set; }

    public RequireSubscriptionAttribute() { }

    public RequireSubscriptionAttribute(string requiredFeature)
    {
        RequiredFeature = requiredFeature;
    }
}
