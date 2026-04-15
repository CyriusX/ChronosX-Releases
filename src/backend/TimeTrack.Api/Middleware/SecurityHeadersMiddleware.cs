namespace TimeTrack.Api.Middleware;

/// <summary>
/// Middleware que adiciona headers de segurança HTTP a todas as respostas
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add security headers before the response starts
        AddSecurityHeaders(context.Response.Headers);

        await _next(context);
    }

    /// <summary>
    /// Adds mandatory security headers to the response
    /// </summary>
    private static void AddSecurityHeaders(IHeaderDictionary headers)
    {
        // HSTS - Disabled for containerized environments (EasyPanel handles SSL termination)
        // Only enable HSTS when the app is directly exposed via HTTPS
        // headers["Strict-Transport-Security"] = "max-age=63072000; includeSubDomains";

        // Prevents MIME type sniffing
        headers["X-Content-Type-Options"] = "nosniff";

        // Prevents clickjacking by disallowing the page to be embedded in iframes
        headers["X-Frame-Options"] = "DENY";

        // Content Security Policy - relaxed for APIs
        // APIs don't serve HTML, so we use a minimal policy
        headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";

        // Controls how much referrer information is sent with requests
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    }
}
