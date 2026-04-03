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
}
