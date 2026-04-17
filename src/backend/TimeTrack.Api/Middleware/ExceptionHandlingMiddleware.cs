using System.Net;
using System.Text.Json;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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
        var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

        switch (exception)
        {
            case UserDeactivatedException ex:
                statusCode = (int)HttpStatusCode.Unauthorized;
                response = new ErrorResponse(ex.Code, ex.Message, traceId);
                errorDetails = $"UserDeactivatedException: {ex.Message}";
                break;
            case DbUpdateException ex when TryGetPostgresException(ex, out var pg):
                statusCode = (int)HttpStatusCode.InternalServerError;
                response = MapPostgresException(pg, traceId);
                errorDetails = $"DbUpdateException({pg.SqlState}): {pg.MessageText}";
                break;
            case PostgresException ex:
                statusCode = (int)HttpStatusCode.InternalServerError;
                response = MapPostgresException(ex, traceId);
                errorDetails = $"PostgresException({ex.SqlState}): {ex.MessageText}";
                break;
            case NpgsqlException ex:
                // Typically: connection failures, timeouts, or pool exhaustion.
                statusCode = (int)HttpStatusCode.ServiceUnavailable;
                response = new ErrorResponse(
                    "database_unavailable",
                    "Database is temporarily unavailable. Please retry shortly.",
                    traceId);
                errorDetails = $"NpgsqlException: {ex.Message}";
                break;
            case ForbiddenException ex:
                statusCode = (int)HttpStatusCode.Forbidden;
                response = new ErrorResponse("forbidden", ex.Reason, traceId);
                errorDetails = $"ForbiddenException: {ex.Reason}";
                break;
            case NotFoundException ex:
                statusCode = (int)HttpStatusCode.NotFound;
                response = new ErrorResponse("not_found", $"{ex.EntityType} with key '{ex.Key}' was not found", traceId);
                errorDetails = $"NotFoundException: {ex.EntityType} with key '{ex.Key}'";
                break;
            case ValidationException ex:
                statusCode = (int)HttpStatusCode.BadRequest;
                response = new ValidationErrorResponse("validation_failed", ex.Errors, traceId);
                errorDetails = $"ValidationException: {string.Join(", ", ex.Errors.Select(e => $"{e.Key}: {string.Join(", ", e.Value)}"))}";
                break;
            case ConflictException ex:
                statusCode = (int)HttpStatusCode.Conflict;
                response = new ErrorResponse(ex.Code, ex.Message, traceId);
                errorDetails = $"ConflictException: {ex.Code} - {ex.Message}";
                break;
            case UnauthorizedAccessException ex:
                statusCode = (int)HttpStatusCode.Unauthorized;
                response = new ErrorResponse("unauthorized", ex.Message, traceId);
                errorDetails = $"UnauthorizedAccessException: {ex.Message}";
                break;
            default:
                statusCode = (int)HttpStatusCode.InternalServerError;
                response = new ErrorResponse("internal_error", "An unexpected error occurred", traceId);
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

    private record ErrorResponse(string Code, string Message, string TraceId);

    private record ValidationErrorResponse(string Code, IDictionary<string, string[]> Errors, string TraceId);

    private static bool TryGetPostgresException(DbUpdateException ex, out PostgresException pg)
    {
        var cur = ex.InnerException;
        while (cur is not null)
        {
            if (cur is PostgresException pe)
            {
                pg = pe;
                return true;
            }
            cur = cur.InnerException;
        }

        pg = null!;
        return false;
    }

    private static ErrorResponse MapPostgresException(PostgresException pg, string traceId) => pg.SqlState switch
    {
        // Likely: API deployed without applying EF migrations (common in containerized deploys).
        "42703" or "42P01" => new ErrorResponse(
            "db_schema_out_of_date",
            "Database schema is out of date. Apply migrations and restart the API.",
            traceId),

        _ => new ErrorResponse("database_error", "A database error occurred", traceId)
    };
}
