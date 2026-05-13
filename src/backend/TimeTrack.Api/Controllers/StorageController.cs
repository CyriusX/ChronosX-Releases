using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TimeTrack.Api.Extensions;
using TimeTrack.Api.Middleware;
using TimeTrack.Api.Security;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Storage.Commands;
using TimeTrack.Backend.Application.Storage.DTOs;
using TimeTrack.Backend.Application.Storage.Queries;

namespace TimeTrack.Api.Controllers;

[ApiController]
[Route("api/v1/orgs/{orgId:guid}/storage")]
[Authorize]
[RequireSubscription]
[EnableRateLimiting(RateLimitingExtensions.PolicyNames.Default)]
public sealed class StorageController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly ICurrentUserContext _currentUser;

    public StorageController(ISender mediator, ICurrentUserContext currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    [HttpGet("usage")]
    [ProducesResponseType(typeof(StorageUsageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<StorageUsageResponse>> GetStorageUsage(
        [FromRoute] Guid orgId,
        CancellationToken ct)
    {
        if (_currentUser.OrgId != orgId)
            return Forbid();

        var query = new GetStorageUsageQuery(orgId);
        var result = await _mediator.Send(query, ct);
        return Ok(result);
    }

    [HttpPut("quota")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    [ProducesResponseType(typeof(StorageUsageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<StorageUsageResponse>> UpdateStorageQuota(
        [FromRoute] Guid orgId,
        [FromBody] UpdateStorageQuotaRequest request,
        CancellationToken ct)
    {
        if (_currentUser.OrgId != orgId)
            return Forbid();

        var command = new UpdateStorageQuotaCommand(orgId, request.QuotaGb);
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }
}
