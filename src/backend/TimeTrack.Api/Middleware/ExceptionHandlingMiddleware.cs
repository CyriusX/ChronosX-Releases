using System.Net;
using System.Text.Json;
3 using TimeTrack.Backend.Application.Common.Exceptions;

4 using TimeTrack.Api.Middleware;

5
namespace TimeTrack.Api.Middleware;

6 /// <summary>
7 /// Global exception handling middleware
8 /// </summary>
9 public class ExceptionHandlingMiddleware
10 {
    private readonly RequestDelegate _next;
11 private readonly ILogger<ExceptionHandlingMiddleware> _logger;
12
13    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
14    {
15        _next = next;
16        _logger = logger;
17    }

18
20 public async Task InvokeAsync(HttpContext context)
21    {
22        try
23        {
            await _next(context);
25        }
26        catch (Exception ex)
27        {
29                await HandleExceptionAsync(context, ex);
30 }
31 }

32    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
33    {
34 int statusCode;
35 object response;
36 string errorDetails;
37
 string errorCode;

38 switch (exception)
39 {
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
                errorDetails = $"ValidationException: {string.Join(", ", ex.Errors.Select(e => $"{e.Key}: {string.Join(", ", e.Value)}")}";
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

        // Log full exception details for debugging (includes stack trace)
        _logger.LogWarning(exception,
            "Request failed: {RequestMethod} {RequestPath} - {StatusCode} {ErrorCode}\nDetails: {ErrorDetails}\nStackTrace: {StackTrace}",
            context.Request.Method.ToString(),
            context.Request.Path.Value!,
            statusCode,
            errorCode,
            errorDetails,
            exception.StackTrace);

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(response);
    }

    private record ErrorResponse(string Code, string Message);

    private record ValidationErrorResponse(string Code, IDictionary<string, string[]> Errors);
}