using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeTrack.Api.Security;
using TimeTrack.Backend.Application.PlatformEvents.DTOs;
using TimeTrack.Backend.Application.PlatformEvents.Queries;

namespace TimeTrack.Api.Controllers;

[ApiController]
[Route("api/v1/platform")]
[Authorize(Policy = AuthorizationPolicies.PlatformAdminOnly)]
public sealed class PlatformEventsController : ControllerBase
{
    private readonly ISender _mediator;

    public PlatformEventsController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("events")]
    [ProducesResponseType(typeof(IReadOnlyList<PlatformEventLogDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PlatformEventLogDto>>> ListEvents(
        [FromQuery] DateTime? sinceUtc = null,
        [FromQuery] string? severity = null,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ListPlatformEventsQuery(sinceUtc, severity, limit), cancellationToken);
        return Ok(result);
    }

    [HttpGet("health")]
    [ProducesResponseType(typeof(PlatformHealthDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PlatformHealthDto>> GetHealth(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPlatformHealthQuery(), cancellationToken);
        return Ok(result);
    }
}

