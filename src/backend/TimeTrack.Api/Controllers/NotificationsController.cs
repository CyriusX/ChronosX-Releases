using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TimeTrack.Api.Extensions;
using TimeTrack.Backend.Application.Notifications;

namespace TimeTrack.Api.Controllers;

[ApiController]
[Route("api/v1/me/notifications")]
[Authorize]
[EnableRateLimiting(RateLimitingExtensions.PolicyNames.Default)]
public sealed class NotificationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public NotificationsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [ProducesResponseType(typeof(ListNotificationsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ListNotificationsResponse>> List([FromQuery] bool unreadOnly = false, [FromQuery] int take = 50)
    {
        var result = await _mediator.Send(new ListMyNotificationsQuery(unreadOnly, take));
        return Ok(result);
    }

    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        await _mediator.Send(new MarkNotificationReadCommand(id));
        return NoContent();
    }

    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllRead()
    {
        await _mediator.Send(new MarkAllNotificationsReadCommand());
        return NoContent();
    }
}
