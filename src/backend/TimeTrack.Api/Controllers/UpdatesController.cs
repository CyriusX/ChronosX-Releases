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
    private readonly IConfiguration _configuration;
    private readonly ILogger<UpdatesController> _logger;

    public UpdatesController(
        IMediator mediator,
        IConfiguration configuration,
        ILogger<UpdatesController> logger)
    {
        _mediator = mediator;
        _configuration = configuration;
        _logger = logger;
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

    /// <summary>
    /// Download the latest installer
    /// </summary>
    [HttpGet("download")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult DownloadInstaller()
    {
        var installerPath = _configuration["Updates:InstallerPath"];

        if (string.IsNullOrEmpty(installerPath) || !System.IO.File.Exists(installerPath))
        {
            _logger.LogWarning("Installer file not found at: {Path}", installerPath ?? "(not configured)");
            return NotFound(new { error = "Installer not available" });
        }

        var fileName = Path.GetFileName(installerPath);
        var fileSize = new FileInfo(installerPath).Length;

        _logger.LogInformation("Serving installer: {File} ({Size:N0} bytes)", fileName, fileSize);

        // Stream the file to avoid loading 90MB into memory
        var stream = new FileStream(installerPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);

        Response.Headers.Append("Accept-Ranges", "bytes");

        return File(stream, "application/octet-stream", fileName, enableRangeProcessing: true);
    }
}
