using System.Text;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TimeTrack.Api.Extensions;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Common.Security;
using TimeTrack.Backend.Application.Common.Utilities;
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
[EnableRateLimiting(RateLimitingExtensions.PolicyNames.Reports)]
public sealed class ReportsController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly IUserAuthorizationService _authorizationService;
    private readonly ICurrentUserContext _currentUser;
    private readonly IAuditLogService _auditLogService;

    public ReportsController(
        ISender mediator,
        IUserAuthorizationService authorizationService,
        ICurrentUserContext currentUser,
        IAuditLogService auditLogService)
    {
        _mediator = mediator;
        _authorizationService = authorizationService;
        _currentUser = currentUser;
        _auditLogService = auditLogService;
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
        var isAccessingOtherUserData = targetUserId != _currentUser.UserId!.Value;

        // Validate authorization - if requesting another user's data
        if (isAccessingOtherUserData)
        {
            var userRole = _currentUser.Role;
            if (userRole != UserRole.Admin && userRole != UserRole.Gestor)
            {
                return Forbid();
            }
        }

        // Audit log - report.accessed (when manager/admin accesses other user's data)
        if (isAccessingOtherUserData)
        {
            _auditLogService.LogAsync(
                AuditActions.ReportAccessed,
                "user",
                targetUserId,
                new { startDate = startDate.ToString("yyyy-MM-dd"), endDate = endDate.ToString("yyyy-MM-dd"), format },
                cancellationToken);
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
    /// Obtém resumo diário de atividade de um usuário
    /// </summary>
    /// <param name="userId">ID do usuário (opcional, padrão é o usuário atual)</param>
    /// <param name="date">Data do resumo (formato YYYY-MM-DD)</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Resumo diário com tempo ativo, idle e apps</returns>
    [HttpGet("daily")]
    [ProducesResponseType(typeof(DailySummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDailySummary(
        [FromQuery] Guid? userId,
        [FromQuery] DateTime date,
        CancellationToken cancellationToken = default)
    {
        // Determine target userId
        var targetUserId = userId ?? _currentUser.UserId!.Value;
        var isAccessingOtherUserData = targetUserId != _currentUser.UserId!.Value;

        // Validate authorization - if requesting another user's data
        if (isAccessingOtherUserData)
        {
            var userRole = _currentUser.Role;
            if (userRole != UserRole.Admin && userRole != UserRole.Gestor)
            {
                return Forbid();
            }

            // Audit log
            _auditLogService.LogAsync(
                AuditActions.ReportAccessed,
                "user",
                targetUserId,
                new { reportType = "daily", date = date.ToString("yyyy-MM-dd") },
                cancellationToken);
        }

        var query = new DailySummaryQuery(
            UserId: targetUserId,
            Date: date
        );

        try
        {
            var response = await _mediator.Send(query, cancellationToken);

            // Generate ETag for caching
            var etag = ETagGenerator.Generate(response);
            Response.Headers.ETag = $"\"{etag}\"";

            // Check If-None-Match for 304
            if (ETagGenerator.Matches(Request.Headers.IfNoneMatch, etag))
            {
                return StatusCode(StatusCodes.Status304NotModified);
            }

            return Ok(response);
        }
        catch (ForbiddenException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// Obtém top apps de um usuário por período
    /// </summary>
    /// <param name="userId">ID do usuário (opcional, padrão é o usuário atual)</param>
    /// <param name="startDate">Data inicial do período</param>
    /// <param name="endDate">Data final do período</param>
    /// <param name="limit">Número máximo de apps a retornar (padrão: 10, máx: 100)</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de apps ordenados por tempo total (desc)</returns>
    [HttpGet("top-apps")]
    [ProducesResponseType(typeof(TopAppsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTopApps(
        [FromQuery] Guid? userId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        // Validate date range
        if (startDate > endDate)
        {
            return BadRequest(new { error = "Start date must be before or equal to end date" });
        }

        // Determine target userId
        var targetUserId = userId ?? _currentUser.UserId!.Value;
        var isAccessingOtherUserData = targetUserId != _currentUser.UserId!.Value;

        // Validate authorization - if requesting another user's data
        if (isAccessingOtherUserData)
        {
            var userRole = _currentUser.Role;
            if (userRole != UserRole.Admin && userRole != UserRole.Gestor)
            {
                return Forbid();
            }

            // Audit log
            _auditLogService.LogAsync(
                AuditActions.ReportAccessed,
                "user",
                targetUserId,
                new { reportType = "top-apps", startDate = startDate.ToString("yyyy-MM-dd"), endDate = endDate.ToString("yyyy-MM-dd"), limit },
                cancellationToken);
        }

        var query = new TopAppsQuery(
            UserId: targetUserId,
            StartDate: startDate,
            EndDate: endDate,
            Limit: limit
        );

        try
        {
            var response = await _mediator.Send(query, cancellationToken);

            // Generate ETag for caching
            var etag = ETagGenerator.Generate(response);
            Response.Headers.ETag = $"\"{etag}\"";

            // Check If-None-Match for 304
            if (ETagGenerator.Matches(Request.Headers.IfNoneMatch, etag))
            {
                return StatusCode(StatusCodes.Status304NotModified);
            }

            return Ok(response);
        }
        catch (ForbiddenException)
        {
            return Forbid();
        }
    }

    // ========================================================================
    // NOVOS ENDPOINTS - CX-155 (Página de Relatório)
    // ========================================================================

    /// <summary>
    /// Obtém resumo diário de um período para heatmap estilo GitHub
    /// </summary>
    /// <param name="userId">ID do usuário (opcional, padrão é o usuário atual)</param>
    /// <param name="startDate">Data inicial do período</param>
    /// <param name="endDate">Data final do período</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de resumos diários</returns>
    [HttpGet("daily-summary-range")]
    [ProducesResponseType(typeof(DailySummaryRangeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDailySummaryRange(
        [FromQuery] Guid? userId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        if (startDate > endDate)
        {
            return BadRequest(new { error = "Start date must be before or equal to end date" });
        }

        var targetUserId = userId ?? _currentUser.UserId!.Value;
        var isAccessingOtherUserData = targetUserId != _currentUser.UserId!.Value;

        if (isAccessingOtherUserData)
        {
            var userRole = _currentUser.Role;
            if (userRole != UserRole.Admin && userRole != UserRole.Gestor)
            {
                return Forbid();
            }

            _auditLogService.LogAsync(
                AuditActions.ReportAccessed,
                "user",
                targetUserId,
                new { reportType = "daily-summary-range", startDate = startDate.ToString("yyyy-MM-dd"), endDate = endDate.ToString("yyyy-MM-dd") },
                cancellationToken);
        }

        var query = new DailySummaryRangeQuery(
            UserId: targetUserId,
            StartDate: startDate,
            EndDate: endDate
        );

        try
        {
            var response = await _mediator.Send(query, cancellationToken);
            return Ok(response);
        }
        catch (ForbiddenException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// Obtém tendência de produtividade por período (gráfico de barras empilhadas)
    /// </summary>
    /// <param name="userId">ID do usuário (opcional, padrão é o usuário atual)</param>
    /// <param name="startDate">Data inicial do período</param>
    /// <param name="endDate">Data final do período</param>
    /// <param name="groupBy">Agrupamento: day, week, month</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de períodos com produtividade</returns>
    [HttpGet("productivity-trend")]
    [ProducesResponseType(typeof(ProductivityTrendResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetProductivityTrend(
        [FromQuery] Guid? userId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] string groupBy = "day",
        CancellationToken cancellationToken = default)
    {
        if (startDate > endDate)
        {
            return BadRequest(new { error = "Start date must be before or equal to end date" });
        }

        var validGroupBy = new[] { "day", "week", "month" };
        if (!validGroupBy.Contains(groupBy.ToLowerInvariant()))
        {
            return BadRequest(new { error = "groupBy must be one of: day, week, month" });
        }

        var targetUserId = userId ?? _currentUser.UserId!.Value;
        var isAccessingOtherUserData = targetUserId != _currentUser.UserId!.Value;

        if (isAccessingOtherUserData)
        {
            var userRole = _currentUser.Role;
            if (userRole != UserRole.Admin && userRole != UserRole.Gestor)
            {
                return Forbid();
            }

            _auditLogService.LogAsync(
                AuditActions.ReportAccessed,
                "user",
                targetUserId,
                new { reportType = "productivity-trend", startDate = startDate.ToString("yyyy-MM-dd"), endDate = endDate.ToString("yyyy-MM-dd"), groupBy },
                cancellationToken);
        }

        var query = new ProductivityTrendQuery(
            UserId: targetUserId,
            StartDate: startDate,
            EndDate: endDate,
            GroupBy: groupBy.ToLowerInvariant()
        );

        try
        {
            var response = await _mediator.Send(query, cancellationToken);
            return Ok(response);
        }
        catch (ForbiddenException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// Obtém top URLs e caminhos extraídos de window_title
    /// </summary>
    /// <param name="userId">ID do usuário (opcional, padrão é o usuário atual)</param>
    /// <param name="startDate">Data inicial do período</param>
    /// <param name="endDate">Data final do período</param>
    /// <param name="limit">Número máximo de paths (padrão: 20)</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista de URLs e caminhos mais acessados</returns>
    [HttpGet("top-paths")]
    [ProducesResponseType(typeof(TopPathsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTopPaths(
        [FromQuery] Guid? userId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (startDate > endDate)
        {
            return BadRequest(new { error = "Start date must be before or equal to end date" });
        }

        var targetUserId = userId ?? _currentUser.UserId!.Value;
        var isAccessingOtherUserData = targetUserId != _currentUser.UserId!.Value;

        if (isAccessingOtherUserData)
        {
            var userRole = _currentUser.Role;
            if (userRole != UserRole.Admin && userRole != UserRole.Gestor)
            {
                return Forbid();
            }

            _auditLogService.LogAsync(
                AuditActions.ReportAccessed,
                "user",
                targetUserId,
                new { reportType = "top-paths", startDate = startDate.ToString("yyyy-MM-dd"), endDate = endDate.ToString("yyyy-MM-dd"), limit },
                cancellationToken);
        }

        var query = new TopPathsQuery(
            UserId: targetUserId,
            StartDate: startDate,
            EndDate: endDate,
            Limit: Math.Clamp(limit, 1, 100)
        );

        try
        {
            var response = await _mediator.Send(query, cancellationToken);
            return Ok(response);
        }
        catch (ForbiddenException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// Obtém estatísticas de distração (top 5 apps + linha do tempo)
    /// </summary>
    /// <param name="userId">ID do usuário (opcional, padrão é o usuário atual)</param>
    /// <param name="startDate">Data inicial do período</param>
    /// <param name="endDate">Data final do período</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Estatísticas de distração</returns>
    [HttpGet("distraction-stats")]
    [ProducesResponseType(typeof(DistractionStatsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDistractionStats(
        [FromQuery] Guid? userId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        if (startDate > endDate)
        {
            return BadRequest(new { error = "Start date must be before or equal to end date" });
        }

        var targetUserId = userId ?? _currentUser.UserId!.Value;
        var isAccessingOtherUserData = targetUserId != _currentUser.UserId!.Value;

        if (isAccessingOtherUserData)
        {
            var userRole = _currentUser.Role;
            if (userRole != UserRole.Admin && userRole != UserRole.Gestor)
            {
                return Forbid();
            }

            _auditLogService.LogAsync(
                AuditActions.ReportAccessed,
                "user",
                targetUserId,
                new { reportType = "distraction-stats", startDate = startDate.ToString("yyyy-MM-dd"), endDate = endDate.ToString("yyyy-MM-dd") },
                cancellationToken);
        }

        var query = new DistractionStatsQuery(
            UserId: targetUserId,
            StartDate: startDate,
            EndDate: endDate
        );

        try
        {
            var response = await _mediator.Send(query, cancellationToken);
            return Ok(response);
        }
        catch (ForbiddenException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// Obtém distribuição por categoria de produtividade (donut chart)
    /// </summary>
    /// <param name="userId">ID do usuário (opcional, padrão é o usuário atual)</param>
    /// <param name="startDate">Data inicial do período</param>
    /// <param name="endDate">Data final do período</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Distribuição por categoria</returns>
    [HttpGet("category-distribution")]
    [ProducesResponseType(typeof(CategoryDistributionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCategoryDistribution(
        [FromQuery] Guid? userId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        if (startDate > endDate)
        {
            return BadRequest(new { error = "Start date must be before or equal to end date" });
        }

        var targetUserId = userId ?? _currentUser.UserId!.Value;
        var isAccessingOtherUserData = targetUserId != _currentUser.UserId!.Value;

        if (isAccessingOtherUserData)
        {
            var userRole = _currentUser.Role;
            if (userRole != UserRole.Admin && userRole != UserRole.Gestor)
            {
                return Forbid();
            }

            _auditLogService.LogAsync(
                AuditActions.ReportAccessed,
                "user",
                targetUserId,
                new { reportType = "category-distribution", startDate = startDate.ToString("yyyy-MM-dd"), endDate = endDate.ToString("yyyy-MM-dd") },
                cancellationToken);
        }

        var query = new CategoryDistributionQuery(
            UserId: targetUserId,
            StartDate: startDate,
            EndDate: endDate
        );

        try
        {
            var response = await _mediator.Send(query, cancellationToken);
            return Ok(response);
        }
        catch (ForbiddenException)
        {
            return Forbid();
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
