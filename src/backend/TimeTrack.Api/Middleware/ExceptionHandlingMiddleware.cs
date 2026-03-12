using System.Net;
using System.Text.Json;
using TimeTrack.Backend.Application.Common.Exceptions;

namespace TimeTrack.Api.Middleware;

/// <summary>
/// Global exception handling middleware
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        int statusCode;
        object response;

        switch (exception)
        {
            case UserDeactivatedException ex:
                statusCode = (int)HttpStatusCode.Unauthorized;
                response = new ErrorResponse(ex.Code, ex.Message);
                break;
            case ForbiddenException ex:
                statusCode = (int)HttpStatusCode.Forbidden;
                response = new ErrorResponse("forbidden", ex.Reason);
                break;
            case NotFoundException ex:
                statusCode = (int)HttpStatusCode.NotFound;
                response = new ErrorResponse("not_found", $"{ex.EntityType} with key '{ex.Key}' was not found");
                break;
            case ValidationException ex:
                statusCode = (int)HttpStatusCode.BadRequest;
                response = new ValidationErrorResponse("validation_failed", ex.Errors);
                break;
            case ConflictException ex:
                statusCode = (int)HttpStatusCode.Conflict;
                response = new ErrorResponse(ex.Code, ex.Message);
                break;
            default:
                statusCode = (int)HttpStatusCode.InternalServerError;
                response = new ErrorResponse("internal_error", "An unexpected error occurred");
                break;
        }

        var errorCode = response switch
        {
            ErrorResponse er => er.Code,
            ValidationErrorResponse ver => ver.Code,
            _ => "unknown"
        };

        _logger.LogWarning(exception, "Request failed: {StatusCode} - {ErrorCode}", statusCode, errorCode);

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(response);
    }

    private record ErrorResponse(string Code, string Message);

    private record ValidationErrorResponse(string Code, IDictionary<string, string[]> Errors);
}
