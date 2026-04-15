using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TimeTrack.Api.Extensions;
using TimeTrack.Api.Security;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Maintenance.Commands;
using TimeTrack.Backend.Application.Maintenance.DTOs;
using TimeTrack.Backend.Application.Maintenance.Queries;

namespace TimeTrack.Api.Controllers;

/// <summary>
/// Controller para manutenção e monitoramento de dispositivos (Admin only)
/// </summary>
[ApiController]
[Route("api/v1/orgs/{orgId:guid}/maintenance")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[EnableRateLimiting(RateLimitingExtensions.PolicyNames.Default)]
public sealed class MaintenanceController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly ICurrentUserContext _currentUser;

    public MaintenanceController(
        ISender mediator,
        ICurrentUserContext currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Get health summary for all devices in the org (counts + alerts)
    /// </summary>
    [HttpGet("health-summary")]
    [ProducesResponseType(typeof(HealthSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<HealthSummaryResponse>> GetHealthSummary(
        Guid orgId,
        CancellationToken cancellationToken)
    {
        if (_currentUser.OrgId != orgId) return Forbid();

        var result = await _mediator.Send(new GetHealthSummaryQuery(orgId), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get machine metrics for a specific device
    /// </summary>
    [HttpGet("devices/{deviceId:guid}/metrics")]
    [ProducesResponseType(typeof(DeviceMetricsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeviceMetricsResponse>> GetDeviceMetrics(
        Guid orgId,
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        if (_currentUser.OrgId != orgId)
        {
            return Forbid();
        }

        var result = await _mediator.Send(
            new GetDeviceMetricsQuery(orgId, deviceId),
            cancellationToken);

        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    /// <summary>
    /// Get agent event logs for a specific device
    /// </summary>
    [HttpGet("devices/{deviceId:guid}/events")]
    [ProducesResponseType(typeof(DeviceEventsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeviceEventsResponse>> GetDeviceEvents(
        Guid orgId,
        Guid deviceId,
        [FromQuery] string? category = null,
        [FromQuery] string? severity = null,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (_currentUser.OrgId != orgId)
        {
            return Forbid();
        }

        var result = await _mediator.Send(
            new GetDeviceEventsQuery(orgId, deviceId, category, severity, Limit: limit),
            cancellationToken);

        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    /// <summary>
    /// Get device info (OS, IP, uptime, tracking state, etc.)
    /// </summary>
    [HttpGet("devices/{deviceId:guid}/info")]
    [ProducesResponseType(typeof(DeviceInfoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeviceInfoResponse>> GetDeviceInfo(
        Guid orgId,
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        if (_currentUser.OrgId != orgId) return Forbid();

        var result = await _mediator.Send(
            new GetDeviceInfoQuery(orgId, deviceId), cancellationToken);

        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Send a remote command to a device
    /// </summary>
    [HttpPost("devices/{deviceId:guid}/commands")]
    [ProducesResponseType(typeof(CreateRemoteCommandResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateRemoteCommandResponse>> CreateCommand(
        Guid orgId,
        Guid deviceId,
        [FromBody] CreateRemoteCommandRequest request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.OrgId != orgId) return Forbid();

        var result = await _mediator.Send(
            new CreateRemoteCommandCommand(orgId, deviceId, request.CommandType, request.Payload),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Clear event logs for a specific device
    /// </summary>
    [HttpDelete("devices/{deviceId:guid}/events")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<int>> ClearDeviceEvents(
        Guid orgId,
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        if (_currentUser.OrgId != orgId) return Forbid();

        var deleted = await _mediator.Send(
            new ClearDeviceEventsCommand(orgId, deviceId), cancellationToken);

        return Ok(new { deleted });
    }

    /// <summary>
    /// Clear event logs for all devices in the org
    /// </summary>
    [HttpDelete("events")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<int>> ClearAllEvents(
        Guid orgId,
        CancellationToken cancellationToken)
    {
        if (_currentUser.OrgId != orgId) return Forbid();

        var deleted = await _mediator.Send(
            new ClearDeviceEventsCommand(orgId, null), cancellationToken);

        return Ok(new { deleted });
    }

    /// <summary>
    /// Get command history for a device
    /// </summary>
    [HttpGet("devices/{deviceId:guid}/commands")]
    [ProducesResponseType(typeof(CommandHistoryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CommandHistoryResponse>> GetCommandHistory(
        Guid orgId,
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        if (_currentUser.OrgId != orgId) return Forbid();

        var result = await _mediator.Send(
            new GetDeviceCommandsQuery(orgId, deviceId), cancellationToken);

        return result == null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Delete a device record permanently (Admin only)
    /// </summary>
    [HttpDelete("devices/{deviceId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDevice(
        Guid orgId,
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        if (_currentUser.OrgId != orgId) return Forbid();

        await _mediator.Send(new DeleteDeviceCommand(orgId, deviceId), cancellationToken);
        return NoContent();
    }
}
