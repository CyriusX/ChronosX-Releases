using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TimeTrack.Api.Extensions;
using TimeTrack.Api.Security;
using TimeTrack.Backend.Application.Maintenance.DTOs;
using TimeTrack.Backend.Application.Maintenance.Queries;

namespace TimeTrack.Api.Controllers;

/// <summary>
/// Controller for platform admins to view and manage all organizations
/// </summary>
[ApiController]
[Route("api/v1/platform/organizations")]
[Authorize(Policy = AuthorizationPolicies.PlatformAdminOnly)]
[EnableRateLimiting(RateLimitingExtensions.PolicyNames.Default)]
public sealed class PlatformOrganizationsController : ControllerBase
{
    private readonly ISender _mediator;

    public PlatformOrganizationsController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// List all organizations (Platform Admin only)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ListOrganizationsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ListOrganizationsResponse>> ListAllOrganizations(
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListAllOrganizationsQuery(), cancellationToken);
        return Ok(result);
    }
}
