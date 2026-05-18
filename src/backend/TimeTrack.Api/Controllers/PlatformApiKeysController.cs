using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeTrack.Api.Security;
using TimeTrack.Backend.Application.PlatformApiKeys.Commands;
using TimeTrack.Backend.Application.PlatformApiKeys.DTOs;
using TimeTrack.Backend.Application.PlatformApiKeys.Queries;

namespace TimeTrack.Api.Controllers;

[ApiController]
[Route("api/v1/platform/api-keys")]
[Authorize(Policy = AuthorizationPolicies.PlatformAdminOnly)]
public sealed class PlatformApiKeysController : ControllerBase
{
    private readonly ISender _mediator;

    public PlatformApiKeysController(ISender mediator)
    {
        _mediator = mediator;
    }

    public sealed class CreatePlatformApiKeyRequest
    {
        public string Label { get; init; } = string.Empty;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PlatformApiKeyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PlatformApiKeyDto>>> List(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListPlatformApiKeysQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreatePlatformApiKeyResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<CreatePlatformApiKeyResponse>> Create(
        [FromBody] CreatePlatformApiKeyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreatePlatformApiKeyCommand(request.Label), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpDelete("{apiKeyId:guid}")]
    [ProducesResponseType(typeof(PlatformApiKeyDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PlatformApiKeyDto>> Revoke(
        Guid apiKeyId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RevokePlatformApiKeyCommand(apiKeyId), cancellationToken);
        return Ok(result);
    }
}

