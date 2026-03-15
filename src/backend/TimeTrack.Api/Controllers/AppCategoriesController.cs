using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TimeTrack.Api.Extensions;
using TimeTrack.Api.Security;
using TimeTrack.Backend.Application.AppCategories.Commands;
using TimeTrack.Backend.Application.AppCategories.DTOs;
using TimeTrack.Backend.Application.AppCategories.Queries;
using TimeTrack.Backend.Application.Common.Interfaces;

namespace TimeTrack.Api.Controllers;

/// <summary>
/// Controller for app categorization management
///
/// CX-143: Sistema de Categorização de Apps/Sites (Lista Global + Override por Org)
///
/// Endpoints:
/// - GET    /api/v1/orgs/{orgId}/app-categories           - Batch lookup (Agent sync)
/// - GET    /api/v1/orgs/{orgId}/app-categories/overrides - List overrides (Admin/Gestor)
/// - PUT    /api/v1/orgs/{orgId}/app-categories/overrides - Upsert override (Admin only)
/// - DELETE /api/v1/orgs/{orgId}/app-categories/overrides/{identifier} - Remove override (Admin only)
/// - GET    /api/v1/orgs/{orgId}/app-categories/global   - Search global (Admin/Gestor)
/// - GET    /api/v1/orgs/{orgId}/app-categories/stats    - Usage stats (Admin/Gestor)
/// </summary>
[ApiController]
[Route("api/v1/orgs/{orgId:guid}/app-categories")]
[Authorize]
[EnableRateLimiting(RateLimitingExtensions.PolicyNames.Default)]
public sealed class AppCategoriesController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly ICurrentUserContext _currentUser;

    public AppCategoriesController(
        ISender mediator,
        ICurrentUserContext currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    // ========================================================================
    // Agent Sync Endpoint (All authenticated users)
    // ========================================================================

    /// <summary>
    /// Get all categories for an organization (merged global + overrides)
    /// Used by Agent for initial sync and cache updates
    /// </summary>
    /// <param name="orgId">Organization ID</param>
    /// <param name="version">Optional version for cache validation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Batch of all categories</returns>
    [HttpGet]
    [ProducesResponseType(typeof(AppCategoryBatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AppCategoryBatchResponse>> GetAllCategories(
        [FromRoute] Guid orgId,
        [FromQuery] int? version = null,
        CancellationToken cancellationToken = default)
    {
        // Verify user belongs to the organization
        if (_currentUser.OrgId != orgId)
        {
            return Forbid();
        }

        var query = new GetAppCategoriesBatchQuery(orgId, version);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    // ========================================================================
    // Override Management (Admin/Gestor)
    // ========================================================================

    /// <summary>
    /// List all category overrides for an organization
    /// </summary>
    /// <param name="orgId">Organization ID</param>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 50, max: 100)</param>
    /// <param name="productivity">Filter by productivity: productive | neutral | distraction</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of overrides</returns>
    [HttpGet("overrides")]
    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [ProducesResponseType(typeof(AppCategoryOverrideListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AppCategoryOverrideListResponse>> ListOverrides(
        [FromRoute] Guid orgId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? productivity = null,
        CancellationToken cancellationToken = default)
    {
        // Verify user belongs to the organization
        if (_currentUser.OrgId != orgId)
        {
            return Forbid();
        }

        var query = new GetAppCategoryOverridesQuery(orgId, page, pageSize, productivity);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Create or update a category override
    /// Admin can override any app's classification for their organization
    /// </summary>
    /// <param name="orgId">Organization ID</param>
    /// <param name="request">Override details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created/updated override</returns>
    [HttpPut("overrides")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(AppCategoryOverrideResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AppCategoryOverrideResponse>> UpsertOverride(
        [FromRoute] Guid orgId,
        [FromBody] UpsertAppCategoryOverrideRequest request,
        CancellationToken cancellationToken = default)
    {
        // Verify user belongs to the organization
        if (_currentUser.OrgId != orgId)
        {
            return Forbid();
        }

        var command = new UpsertAppCategoryOverrideCommand(orgId, request);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Delete a category override (revert to global classification)
    /// </summary>
    /// <param name="orgId">Organization ID</param>
    /// <param name="identifier">App identifier to remove override for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success status</returns>
    [HttpDelete("overrides/{identifier}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteOverride(
        [FromRoute] Guid orgId,
        [FromRoute] string identifier,
        CancellationToken cancellationToken = default)
    {
        // Verify user belongs to the organization
        if (_currentUser.OrgId != orgId)
        {
            return Forbid();
        }

        var command = new DeleteAppCategoryOverrideCommand(orgId, identifier);
        var deleted = await _mediator.Send(command, cancellationToken);

        if (!deleted)
        {
            return NotFound(new { error = "Override not found" });
        }

        return NoContent();
    }

    // ========================================================================
    // Global Category Search (Admin/Gestor)
    // ========================================================================

    /// <summary>
    /// Search global categories (for Admin to choose what to override)
    /// Shows which apps already have overrides
    /// </summary>
    /// <param name="orgId">Organization ID</param>
    /// <param name="search">Search term (identifier or display name)</param>
    /// <param name="productivity">Filter by productivity: productive | neutral | distraction</param>
    /// <param name="limit">Max results (default: 50, max: 100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Matching global categories with override status</returns>
    [HttpGet("global")]
    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [ProducesResponseType(typeof(GlobalCategorySearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<GlobalCategorySearchResponse>> SearchGlobal(
        [FromRoute] Guid orgId,
        [FromQuery] string? search = null,
        [FromQuery] string? productivity = null,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        // Verify user belongs to the organization
        if (_currentUser.OrgId != orgId)
        {
            return Forbid();
        }

        var query = new SearchGlobalCategoriesQuery(orgId, search, productivity, limit);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    // ========================================================================
    // Usage Statistics (Admin/Gestor Dashboard)
    // ========================================================================

    /// <summary>
    /// Get category usage statistics for the organization
    /// Shows top apps per category and uncategorized apps needing attention
    /// </summary>
    /// <param name="orgId">Organization ID</param>
    /// <param name="startDate">Start date (default: 30 days ago)</param>
    /// <param name="endDate">End date (default: today)</param>
    /// <param name="limit">Top N per category (default: 10, max: 50)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Usage statistics and uncategorized apps</returns>
    [HttpGet("stats")]
    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [ProducesResponseType(typeof(CategoryUsageStatsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CategoryUsageStatsResponse>> GetUsageStats(
        [FromRoute] Guid orgId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        // Verify user belongs to the organization
        if (_currentUser.OrgId != orgId)
        {
            return Forbid();
        }

        // Default to last 30 days
        var effectiveStartDate = startDate ?? DateTime.UtcNow.AddDays(-30);
        var effectiveEndDate = endDate ?? DateTime.UtcNow;

        var query = new GetCategoryUsageStatsQuery(orgId, effectiveStartDate, effectiveEndDate, limit);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }
}
