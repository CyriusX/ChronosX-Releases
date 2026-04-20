using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeTrack.Api.Security;
using TimeTrack.Backend.Application.PlatformAdmin.Commands;

namespace TimeTrack.Api.Controllers;

/// <summary>
/// Platform-level operations reserved for Chronos staff. Callers must have
/// the is_platform_admin claim. These endpoints are not part of the billing
/// check — platform admins already bypass it in SubscriptionCheckMiddleware.
/// </summary>
[ApiController]
[Route("api/v1/platform/admins")]
[Authorize(Policy = AuthorizationPolicies.PlatformAdminOnly)]
public sealed class PlatformAdminController : ControllerBase
{
    private readonly ISender _mediator;

    public PlatformAdminController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Grant or revoke platform-admin status on a user.</summary>
    [HttpPost("{userId:guid}")]
    [ProducesResponseType(typeof(SetPlatformAdminResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SetPlatformAdminResponse>> SetPlatformAdmin(
        Guid userId,
        [FromBody] SetPlatformAdminRequest body,
        CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(
            new SetPlatformAdminCommand(userId, body.IsPlatformAdmin),
            cancellationToken);
        return Ok(response);
    }
}

public sealed class SetPlatformAdminRequest
{
    public bool IsPlatformAdmin { get; init; }
}
