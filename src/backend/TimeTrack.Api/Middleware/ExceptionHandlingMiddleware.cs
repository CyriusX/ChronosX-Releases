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
        string errorDetails;

        switch (exception)
        {
            case UserDeactivatedException ex:
                statusCode = (int)HttpStatusCode.Unauthorized;
                response = new ErrorResponse(ex.Code, ex.Message);
                errorDetails = $"UserDeactivatedException: {ex.Message}";
                break;
            case ForbiddenException ex:
                statusCode = (int)HttpStatusCode.Forbidden;
                response = new ErrorResponse("forbidden", ex.Reason);
                errorDetails = $"ForbiddenException: {ex.Reason}";
                break;
            case NotFoundException ex:
                statusCode = (int)HttpStatusCode.NotFound;
                response = new ErrorResponse("not_found", $"{ex.EntityType} with key '{ex.Key}' was not found");
                errorDetails = $"NotFoundException: {ex.EntityType} with key '{ex.Key}'";
                break;
            case ValidationException ex:
                statusCode = (int)HttpStatusCode.BadRequest;
                response = new ValidationErrorResponse("validation_failed", ex.Errors);
                errorDetails = $"ValidationException: {string.Join(", ", ex.Errors.Select(e => $"{e.Key}: {string.Join(", ", e.Value)}"))}";
                break;
            case ConflictException ex:
                statusCode = (int)HttpStatusCode.Conflict;
                response = new ErrorResponse(ex.Code, ex.Message);
                errorDetails = $"ConflictException: {ex.Code} - {ex.Message}";
                break;
            default:
                statusCode = (int)HttpStatusCode.InternalServerError;
                response = new ErrorResponse("internal_error", "An unexpected error occurred");
                errorDetails = $"Unhandled exception: {exception.GetType().Name}: {exception.Message}";
                break;
        }

        var errorCode = response switch
        {
            ErrorResponse er => er.Code,
            ValidationErrorResponse ver => ver.Code,
            _ => "unknown"
        };

        // Log full exception details for debugging
        _logger.LogError(exception,
            "Request failed: {RequestMethod} {RequestPath} - {StatusCode} {ErrorCode}\nExceptionType: {ExceptionType}\nMessage: {Message}\nStack Trace: {StackTrace}",
            context.Request.Method,
            context.Request.Path.Value,
            statusCode,
            errorCode,
            exception.GetType().Name,
            exception.Message,
            exception.StackTrace);

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(response);
    }

    private record ErrorResponse(string Code, string Message);

    private record ValidationErrorResponse(string Code, IDictionary<string, string[]> Errors);
}
