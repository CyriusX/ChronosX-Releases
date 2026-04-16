using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TimeTrack.Api.Extensions;
using TimeTrack.Api.Security;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Policies.Commands;
using TimeTrack.Backend.Application.Policies.DTOs;
using TimeTrack.Backend.Application.Policies.Queries;

namespace TimeTrack.Api.Controllers;

/// <summary>
/// Controller for organization policies
/// </summary>
[ApiController]
[Route("api/v1/orgs/{orgId:guid}/policies")]
[Authorize]
[EnableRateLimiting(RateLimitingExtensions.PolicyNames.Default)]
public sealed class OrgPoliciesController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly ICurrentUserContext _currentUser;

    public OrgPoliciesController(
        ISender mediator,
        ICurrentUserContext currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Gets the organization's policy
    /// </summary>
    /// <param name="orgId">Organization ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The organization's policy</returns>
    [HttpGet]
    [ProducesResponseType(typeof(OrgPolicyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OrgPolicyResponse>> GetPolicy(
        [FromRoute] Guid orgId,
        CancellationToken cancellationToken)
    {
        // Verify user belongs to the organization
        if (_currentUser.OrgId != orgId)
        {
            return Forbid();
        }

        var query = new GetOrgPolicyQuery(orgId);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Updates the organization's policy (Admin/Gestor)
    /// </summary>
    /// <param name="orgId">Organization ID</param>
    /// <param name="request">Policy update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated policy</returns>
    [HttpPut]
    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    [ProducesResponseType(typeof(OrgPolicyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OrgPolicyResponse>> UpdatePolicy(
        [FromRoute] Guid orgId,
        [FromBody] UpdateOrgPolicyRequest request,
        CancellationToken cancellationToken)
    {
        // Verify user belongs to the organization
        if (_currentUser.OrgId != orgId)
        {
            return Forbid();
        }

        var command = new UpdateOrgPolicyCommand(orgId, request);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}
