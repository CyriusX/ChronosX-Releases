using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TimeTrack.Api.Extensions;
using TimeTrack.Api.Middleware;
using TimeTrack.Api.Security;
using TimeTrack.Backend.Application.Audit.DTOs;
using TimeTrack.Backend.Application.Audit.Queries;
using TimeTrack.Backend.Application.Audit.DTOs;
using TimeTrack.Backend.Application.Common.Interfaces;

namespace TimeTrack.Api.Controllers;

/// <summary>
/// Controller para consulta de audit logs
/// </summary>
[ApiController]
[Route("api/v1/orgs/{orgId:guid}/audit")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[RequireSubscription]
[EnableRateLimiting(RateLimitingExtensions.PolicyNames.Default)]
public sealed class AuditController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly ICurrentUserContext _currentUser;

    public AuditController(
        ISender mediator,
        ICurrentUserContext currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Lista audit logs da organização (apenas Admin)
    /// </summary>
    /// <param name="orgId">ID da organização</param>
    /// <param name="page">Página (padrão: 1)</param>
    /// <param name="limit">Itens por página (padrão: 50, máx: 100)</param>
    /// <param name="action">Filtrar por ação (opcional)</param>
    /// <param name="userId">Filtrar por usuário (opcional)</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>Lista paginada de audit logs</returns>
    [HttpGet]
    [ProducesResponseType(typeof(AuditLogListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuditLogListResponse>> GetAuditLogs(
        [FromRoute] Guid orgId,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 50,
        [FromQuery] string? action = null,
        [FromQuery] Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        // Verify user belongs to the organization
        if (_currentUser.OrgId != orgId)
        {
            return Forbid();
        }

        var query = new GetAuditLogsQuery(
            OrgId: orgId,
            Page: page,
            Limit: limit,
            Action: action,
            UserId: userId);

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Lista audit logs de acesso a evidências (apenas Admin)
    /// </summary>
    [HttpGet("evidence-access")]
    [ProducesResponseType(typeof(EvidenceAccessLogResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<EvidenceAccessLogResponse>> GetEvidenceAccessLogs(
        [FromRoute] Guid orgId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] Guid? actorUserId = null,
        [FromQuery] Guid? targetUserId = null,
        [FromQuery] string? action = null,
        CancellationToken cancellationToken = default)
    {
        if (_currentUser.OrgId != orgId)
            return Forbid();

        var query = new GetEvidenceAccessLogsQuery(
            OrgId: orgId,
            Page: page,
            PageSize: pageSize,
            StartDate: startDate,
            EndDate: endDate,
            ActorUserId: actorUserId,
            TargetUserId: targetUserId,
            Action: action);

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }
}
