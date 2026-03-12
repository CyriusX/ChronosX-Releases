using System.Text;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Common.Security;
using TimeTrack.Backend.Application.Reports.DTOs;
using TimeTrack.Backend.Application.Reports.Queries;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Api.Controllers;

/// <summary>
/// Controller de relatórios e exportação de dados
/// </summary>
[ApiController]
[Route("api/v1/reports")]
[Authorize]
public sealed class ReportsController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly IUserAuthorizationService _authorizationService;
    private readonly ICurrentUserContext _currentUser;

    public ReportsController(
        ISender mediator,
        IUserAuthorizationService authorizationService,
        ICurrentUserContext currentUser)
    {
        _mediator = mediator;
        _authorizationService = authorizationService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Exporta dados de atividade em formato CSV com streaming
    /// </summary>
    /// <param name="startDate">Data inicial do período</param>
    /// <param name="endDate">Data final do período</param>
    /// <param name="userId">ID do usuário (opcional, apenas Gestor/Admin podem exportar de outros)</param>
    /// <param name="format">Formato de exportação (atualmente apenas 'csv')</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>CSV file with streaming response</returns>
    [HttpGet("export")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] Guid? userId = null,
        [FromQuery] string format = "csv",
        CancellationToken cancellationToken = default)
    {
        // Validate format
        if (format?.ToLower() != "csv")
        {
            return BadRequest(new { error = "Only CSV format is supported" });
        }

        // Validate date range
        if (startDate > endDate)
        {
            return BadRequest(new { error = "Start date must be before or equal to end date" });
        }

        // Determine target userId
        var targetUserId = userId ?? _currentUser.UserId!.Value;

        // Validate authorization - if requesting another user's data
        if (targetUserId != _currentUser.UserId!.Value)
        {
            var userRole = _currentUser.Role;
            if (userRole != UserRole.Admin && userRole != UserRole.Gestor)
            {
                return Forbid();
            }
        }

        // Create query
        var query = new ExportCsvQuery(
            StartDate: startDate,
            EndDate: endDate,
            UserId: targetUserId,
            Format: format
        );

        try
        {
            // Get streaming data
            var rows = await _mediator.Send(query, cancellationToken);

            // Set response headers for CSV download
            var fileName = $"timetrack-export-{DateTime.UtcNow:yyyy-MM-dd}.csv";
            Response.Headers.ContentDisposition = $"attachment; filename=\"{fileName}\"";
            Response.ContentType = "text/csv; charset=utf-8";

            // Write UTF-8 BOM for Excel compatibility
            await Response.Body.WriteAsync(Encoding.UTF8.GetPreamble(), cancellationToken);

            // Write header
            var header = "data,app_display_name,tempo_total_s,sessoes_count\n";
            await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(header), cancellationToken);

            // Stream rows directly to response
            await foreach (var row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var line = $"{row.Data:yyyy-MM-dd},{EscapeCsvField(row.AppDisplayName)},{row.TempoTotalSegundos},{row.SessoesCount}\n";
                await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(line), cancellationToken);
            }

            await Response.Body.FlushAsync(cancellationToken);
            return new EmptyResult();
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499, new { error = "Request cancelled" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"Export failed: {ex.Message}" });
        }
    }

    /// <summary>
    /// Escapa campos CSV para evitar injection
    /// </summary>
    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field))
            return string.Empty;

        // If contains comma, newline, or quote, wrap in quotes and escape quotes
        if (field.Contains(',') || field.Contains('\n') || field.Contains('"'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }
}
