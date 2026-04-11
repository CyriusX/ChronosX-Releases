using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TimeTrack.Api.Extensions;
using TimeTrack.Backend.Application.Integrations.DTOs;
using TimeTrack.Backend.Application.Integrations.Linear.Commands;
using TimeTrack.Backend.Application.Integrations.Linear.Queries;

namespace TimeTrack.Api.Controllers;

[ApiController]
[Authorize]
[EnableRateLimiting(RateLimitingExtensions.PolicyNames.Default)]
public sealed class UserIntegrationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public UserIntegrationsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("api/v1/me/integrations")]
    [ProducesResponseType(typeof(ListUserIntegrationsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ListUserIntegrationsResponse>> ListMyIntegrations()
    {
        var result = await _mediator.Send(new ListMyIntegrationsQuery());
        return Ok(result);
    }

    [HttpPost("api/v1/me/integrations/linear/connect")]
    [ProducesResponseType(typeof(UserIntegrationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserIntegrationResponse>> ConnectLinear([FromBody] ConnectLinearRequest request)
    {
        var result = await _mediator.Send(new ConnectLinearCommand(request.ApiKey));
        return Ok(result);
    }

    [HttpDelete("api/v1/me/integrations/linear")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DisconnectLinear()
    {
        await _mediator.Send(new DisconnectLinearCommand());
        return NoContent();
    }

    [HttpPost("api/v1/me/integrations/linear/sync")]
    [ProducesResponseType(typeof(LinearSyncResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LinearSyncResultResponse>> SyncLinear()
    {
        var result = await _mediator.Send(new SyncFromLinearCommand());
        return Ok(result);
    }

    [HttpGet("api/v1/me/integrations/linear/history")]
    [ProducesResponseType(typeof(ListLinearSyncHistoryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ListLinearSyncHistoryResponse>> ListLinearHistory()
    {
        var result = await _mediator.Send(new ListLinearSyncHistoryQuery());
        return Ok(result);
    }
}
