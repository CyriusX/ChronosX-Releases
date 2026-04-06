using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TimeTrack.Api.Extensions;
using TimeTrack.Backend.Application.Updates.DTOs;
using TimeTrack.Backend.Application.Updates.Queries;

namespace TimeTrack.Api.Controllers;

/// <summary>
/// Controller for application update operations
/// </summary>
[ApiController]
[Route("api/v1/updates")]
[EnableRateLimiting(RateLimitingExtensions.PolicyNames.Default)]
public sealed class UpdatesController : ControllerBase
{
    private readonly IMediator _mediator;

    public UpdatesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Check for available updates
    /// </summary>
    /// <param name="currentVersion">Current application version</param>
    /// <param name="channel">Update channel (stable, beta, alpha)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Update information if an update is available</returns>
    [HttpGet("check")]
    [ProducesResponseType(typeof(UpdateCheckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CheckForUpdates(
        [FromQuery] string currentVersion,
        [FromQuery] string channel = "stable",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(currentVersion))
        {
            return BadRequest(new { error = "currentVersion is required" });
        }

        var result = await _mediator.Send(
            new CheckForUpdatesQuery(currentVersion, channel),
            cancellationToken);

        if (result is null)
        {
            return NoContent();
        }

        return Ok(result);
    }
}
