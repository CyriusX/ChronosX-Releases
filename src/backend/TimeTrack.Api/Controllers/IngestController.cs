using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeTrack.Api.Security;
using TimeTrack.Backend.Application.Ingest.Commands;
using TimeTrack.Backend.Application.Ingest.DTOs;

namespace TimeTrack.Api.Controllers;

/// <summary>
/// Controller for ingesting time tracking data from agents
/// </summary>
[ApiController]
[Route("api/v1/ingest")]
[Authorize]
public sealed class IngestController : ControllerBase
{
    private readonly ISender _mediator;

    public IngestController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Ingest activity sessions from the agent
    /// </summary>
    /// <param name="request">Batch of activity sessions</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processing result</returns>
    [HttpPost("activity-sessions")]
    [ProducesResponseType(typeof(IngestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<IngestResponse>> IngestActivitySessions(
        [FromBody] ActivitySessionIngestRequest request,
        CancellationToken cancellationToken)
    {
        // Payload size check (2MB limit)
        if (Request.ContentLength > 2 * 1024 * 1024)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new
            {
                error = "PayloadTooLarge",
                message = "Payload size cannot exceed 2MB"
            });
        }

        var command = new IngestActivitySessionsCommand
        {
            Items = request.Items
        };

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Ingest idle periods from the agent
    /// </summary>
    /// <param name="request">Batch of idle periods</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processing result</returns>
    [HttpPost("idle-periods")]
    [ProducesResponseType(typeof(IngestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<IngestResponse>> IngestIdlePeriods(
        [FromBody] IdlePeriodIngestRequest request,
        CancellationToken cancellationToken)
    {
        // Payload size check (2MB limit)
        if (Request.ContentLength > 2 * 1024 * 1024)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new
            {
                error = "PayloadTooLarge",
                message = "Payload size cannot exceed 2MB"
            });
        }

        var command = new IngestIdlePeriodsCommand
        {
            Items = request.Items
        };

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}
