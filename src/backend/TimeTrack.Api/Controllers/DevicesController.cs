using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using TimeTrack.Api.Extensions;
using TimeTrack.Backend.Application.Auth.Commands;
using TimeTrack.Backend.Application.Auth.DTOs;

namespace TimeTrack.Api.Controllers;

/// <summary>
/// Controller de dispositivos
/// </summary>
[ApiController]
[Route("api/v1/devices")]
[Authorize]
[EnableRateLimiting(RateLimitingExtensions.PolicyNames.Default)]
public sealed class DevicesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<DevicesController> _logger;

    public DevicesController(IMediator mediator, ILogger<DevicesController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Ativa um dispositivo após instalação
    /// </summary>
    [HttpPost("activate")]
    [ProducesResponseType(typeof(ActivateDeviceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ActivateDeviceResponse>> Activate([FromBody] ActivateDeviceRequest request)
    {
        // Log context (no secrets) to make production troubleshooting easier.
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        var orgId = User.FindFirst("org_id")?.Value;
        _logger.LogInformation(
            "Device activation request: userId={UserId} orgId={OrgId} deviceId={DeviceId} hostname={Hostname} agentVersion={AgentVersion} displayMode={DisplayMode}",
            userId,
            orgId,
            request.DeviceId,
            request.Hostname,
            request.AgentVersion,
            request.DisplayMode);

        var result = await _mediator.Send(new ActivateDeviceCommand(
            request.DeviceId,
            request.Hostname,
            request.DeviceName,
            request.AgentVersion,
            request.DisplayMode));
        return Ok(result);
    }

    /// <summary>
    /// Registra heartbeat do dispositivo (chamado a cada 5 min)
    /// </summary>
    [HttpPost("{deviceId:guid}/heartbeat")]
    [ProducesResponseType(typeof(HeartbeatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HeartbeatResponse>> Heartbeat(
        Guid deviceId,
        [FromBody] HeartbeatRequest? request = null)
    {
        var result = await _mediator.Send(new HeartbeatCommand(
            deviceId,
            request?.AgentVersion,
            request?.OsVersion,
            request?.IpAddress,
            request?.UptimeSeconds,
            request?.TrackingState,
            request?.HealthStatus,
            request?.BackendReachable,
            request?.ConsecutiveSyncFailures,
            request?.LastSuccessfulSyncAt,
            request?.IpcConnected));
        return Ok(result);
    }

    /// <summary>
    /// Get pending remote commands for this device (agent polls this)
    /// </summary>
    [HttpGet("{deviceId:guid}/pending-commands")]
    [ProducesResponseType(typeof(Backend.Application.Maintenance.DTOs.PendingCommandsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<Backend.Application.Maintenance.DTOs.PendingCommandsResponse>> GetPendingCommands(
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new Backend.Application.Maintenance.Queries.GetPendingCommandsQuery(deviceId),
            cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Acknowledge a remote command (agent calls this after execution)
    /// </summary>
    [HttpPost("{deviceId:guid}/commands/{commandId:guid}/ack")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AcknowledgeCommand(
        Guid deviceId,
        Guid commandId,
        [FromBody] Backend.Application.Maintenance.DTOs.AcknowledgeCommandRequest request,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(
            new Backend.Application.Maintenance.Commands.AcknowledgeCommandCommand(commandId, request.Status, request.ResultJson),
            cancellationToken);
        return Ok();
    }

    /// <summary>
    /// Lista dispositivos de uma organização
    /// </summary>
    [HttpGet("/api/v1/orgs/{orgId:guid}/devices")]
    [ProducesResponseType(typeof(ListDevicesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ListDevicesResponse>> ListByOrg(Guid orgId)
    {
        var result = await _mediator.Send(new ListDevicesCommand(orgId));
        return Ok(result);
    }
}
