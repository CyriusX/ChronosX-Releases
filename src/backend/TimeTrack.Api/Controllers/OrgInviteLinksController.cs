using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeTrack.Backend.Application.Auth.DTOs;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.InviteLinks.Commands;
using TimeTrack.Backend.Application.InviteLinks.DTOs;
using TimeTrack.Backend.Application.InviteLinks.Queries;

namespace TimeTrack.Api.Controllers;

[ApiController]
[Route("api/v1/invite-links")]
public sealed class OrgInviteLinksController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserContext _currentUser;

    public OrgInviteLinksController(IMediator mediator, ICurrentUserContext currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Gestor")]
    [ProducesResponseType(typeof(ListInviteLinksResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ListInviteLinksResponse>> List(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListOrgInviteLinksQuery(_currentUser.OrgId!.Value), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Gestor")]
    [ProducesResponseType(typeof(GenerateInviteLinkResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<GenerateInviteLinkResponse>> Generate(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GenerateOrgInviteLinkCommand(_currentUser.OrgId!.Value, _currentUser.UserId!.Value),
            cancellationToken);

        return CreatedAtAction(nameof(List), result);
    }

    [HttpDelete("{linkId:guid}")]
    [Authorize(Roles = "Admin,Gestor")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Revoke(Guid linkId, CancellationToken cancellationToken)
    {
        await _mediator.Send(new RevokeOrgInviteLinkCommand(linkId, _currentUser.OrgId!.Value), cancellationToken);
        return NoContent();
    }

    [HttpGet("{token}/info")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(InviteLinkInfoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InviteLinkInfoResponse>> GetInfo(string token, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetOrgInviteLinkInfoQuery(token), cancellationToken);
        return Ok(result);
    }

    [HttpPost("{token}/register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LoginResponse>> RegisterViaLink(
        string token,
        [FromBody] RegisterViaInviteLinkRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new RegisterViaInviteLinkCommand(token, request.DisplayName, request.Email, request.Password),
            cancellationToken);

        return Ok(result);
    }
}
