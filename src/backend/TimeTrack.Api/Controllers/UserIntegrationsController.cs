using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TimeTrack.Api.Extensions;
using TimeTrack.Backend.Application.Common.Exceptions;
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

    [HttpGet("api/v1/me/integrations/linear/oauth")]
    [ProducesResponseType(typeof(InitiateLinearOAuthResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<InitiateLinearOAuthResponse>> InitiateLinearOAuth()
    {
        var result = await _mediator.Send(new InitiateLinearOAuthQuery());
        return Ok(result);
    }

    [HttpGet("api/v1/me/integrations/linear/oauth-callback")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LinearOAuthCallback([FromQuery] string code, [FromQuery] string state)
    {
        try
        {
            var result = await _mediator.Send(new ExchangeLinearOAuthCodeCommand(code, state));
            return Content("""
                <!DOCTYPE html>
                <html><head><title>Linear Connected</title></head>
                <body style="display:flex;justify-content:center;align-items:center;height:100vh;font-family:sans-serif">
                    <div style="text-align:center">
                        <h2>Linear Connected Successfully!</h2>
                        <p>You can close this window and return to TimeTrack.</p>
                    </div>
                </body></html>
                """, "text/html");
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
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
