using System.Net;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Infrastructure.Jobs.Dashboard;

/// <summary>
/// Authorization filter for Hangfire Dashboard
/// Restricts access to Admin role only
/// </summary>
public sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    private readonly IWebHostEnvironment? _hostEnvironment;

    public HangfireDashboardAuthorizationFilter(IWebHostEnvironment? hostEnvironment = null)
    {
        _hostEnvironment = hostEnvironment;
    }

    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // In development, allow access without authentication
        if (_hostEnvironment?.IsDevelopment() == true)
        {
            return true;
        }

        // In production, require Admin role
        var user = httpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            SetChallengeResponse(httpContext);
            return false;
        }

        // Check if user has Admin role
        var isAdmin = user.Claims.Any(c =>
            c.Type == "role" && string.Equals(c.Value, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase));

        if (!isAdmin)
        {
            SetForbiddenResponse(httpContext);
            return false;
        }

        return true;
    }

    private static void SetChallengeResponse(HttpContext? httpContext)
    {
        if (httpContext == null) return;

        httpContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
        httpContext.Response.Headers.WWWAuthenticate = "Bearer";
    }

    private static void SetForbiddenResponse(HttpContext? httpContext)
    {
        if (httpContext == null) return;

        httpContext.Response.StatusCode = (int)HttpStatusCode.Forbidden;
    }
}
