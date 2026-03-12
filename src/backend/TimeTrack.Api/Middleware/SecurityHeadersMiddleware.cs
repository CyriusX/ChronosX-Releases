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
        // HSTS - HTTP Strict Transport Security
        // max-age=63072000 = 2 years in seconds
        // includeSubDomains applies the policy to all subdomains
        headers["Strict-Transport-Security"] = "max-age=63072000; includeSubDomains";

        // Prevents MIME type sniffing
        headers["X-Content-Type-Options"] = "nosniff";

        // Prevents clickjacking by disallowing the page to be embedded in iframes
        headers["X-Frame-Options"] = "DENY";

        // Content Security Policy - restricts resource loading to same origin
        // For APIs, this is a simple policy since we don't serve HTML
        headers["Content-Security-Policy"] = "default-src 'self'";

        // Controls how much referrer information is sent with requests
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    }
}
