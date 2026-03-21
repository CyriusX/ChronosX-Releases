using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TimeTrack.Api.Extensions;
using TimeTrack.Api.Security;
using TimeTrack.Backend.Application.AppCategories.DTOs;
using TimeTrack.Backend.Application.AppCategories.Queries;
using TimeTrack.Backend.Application.Common.Interfaces;

namespace TimeTrack.Api.Controllers;

/// <summary>
/// Simplified app categories endpoint for Agent sync
/// Uses orgId from JWT token instead of URL parameter
///
/// CX-143: Sistema de Categorização de Apps/Sites
/// </summary>
[ApiController]
[Route("api/v1/app-categories")]
[Authorize]
[EnableRateLimiting(RateLimitingExtensions.PolicyNames.Default)]
public sealed class AgentAppCategoriesController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly ICurrentUserContext _currentUser;

    public AgentAppCategoriesController(
        ISender mediator,
        ICurrentUserContext currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Get all categories for the current user's organization (merged global + overrides)
    /// Used by Agent for initial sync and cache updates
    /// </summary>
    /// <param name="version">Optional version for cache validation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Batch of all categories</returns>
    [HttpGet]
    [ProducesResponseType(typeof(AppCategoryBatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AppCategoryBatchResponse>> GetAllCategories(
        [FromQuery] int? version = null,
        CancellationToken cancellationToken = default)
    {
        // Get orgId from JWT token
        if (!_currentUser.OrgId.HasValue)
        {
            return Forbid("Organization ID not found in token");
        }

        var orgId = _currentUser.OrgId.Value;
        var query = new GetAppCategoriesBatchQuery(orgId, version);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }
}
