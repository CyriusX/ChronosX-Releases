using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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

    public DevicesController(IMediator mediator)
    {
        _mediator = mediator;
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
            request?.AgentVersion));
        return Ok(result);
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
