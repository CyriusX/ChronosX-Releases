using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Onboarding.Commands;
using TimeTrack.Backend.Application.Onboarding.DTOs;
using TimeTrack.Backend.Application.Onboarding.Queries;

namespace TimeTrack.Api.Controllers;

[ApiController]
[Route("api/v1/onboarding")]
[Authorize]
public sealed class OnboardingController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserContext _currentUser;

    public OnboardingController(IMediator mediator, ICurrentUserContext currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    [HttpGet("status")]
    [ProducesResponseType(typeof(OnboardingStatusResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<OnboardingStatusResponse>> GetStatus(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetOnboardingStatusQuery(_currentUser.OrgId!.Value), cancellationToken);
        return Ok(result);
    }

    [HttpPost("complete")]
    [ProducesResponseType(typeof(CompleteOnboardingResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CompleteOnboardingResponse>> Complete(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CompleteOnboardingCommand(_currentUser.OrgId!.Value), cancellationToken);
        return Ok(result);
    }
}
