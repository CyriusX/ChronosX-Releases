using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using TimeTrack.Api.Insights.Queries;
using TimeTrack.Api.Security;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Common.Security;

namespace TimeTrack.Api.Controllers;

[ApiController]
[Route("api/v1/reports")]
[Authorize]
public sealed class InsightsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserContext _currentUser;
    private readonly IUserAuthorizationService _authorizationService;

    public InsightsController(
        IMediator mediator,
        ICurrentUserContext currentUser,
        IUserAuthorizationService authorizationService)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        _authorizationService = authorizationService;
    }

    [HttpGet("daily-insight")]
    public async Task<IActionResult> GetDailyInsight(
        [FromQuery] string? date = null,
        CancellationToken ct = default)
    {
        if (_currentUser.UserId == null || _currentUser.OrgId == null)
            return Forbid();

        var result = await _mediator.Send(
            new GetDailyInsightQuery(date, _currentUser.UserId.Value, _currentUser.OrgId.Value), ct);
        return Ok(result);
    }

    [HttpGet("weekly-narrative")]
    public async Task<IActionResult> GetWeeklyNarrative(
        [FromQuery] string? weekStart = null,
        CancellationToken ct = default)
    {
        if (_currentUser.UserId == null)
            return Forbid();

        var result = await _mediator.Send(
            new GetWeeklyNarrativeQuery(weekStart, _currentUser.UserId.Value), ct);
        return Ok(result);
    }

    [HttpGet("patterns")]
    public async Task<IActionResult> GetPatterns(CancellationToken ct = default)
    {
        if (_currentUser.UserId == null)
            return Forbid();

        var result = await _mediator.Send(
            new GetPatternsQuery(_currentUser.UserId.Value), ct);
        return Ok(result);
    }

    [HttpGet("focus-trend")]
    public async Task<IActionResult> GetFocusTrend(CancellationToken ct = default)
    {
        if (_currentUser.UserId == null)
            return Forbid();

        var result = await _mediator.Send(
            new GetFocusTrendQuery(_currentUser.UserId.Value), ct);
        return Ok(result);
    }

    [HttpGet("benchmark")]
    public async Task<IActionResult> GetBenchmark(CancellationToken ct = default)
    {
        if (_currentUser.UserId == null || _currentUser.OrgId == null)
            return Forbid();

        var result = await _mediator.Send(
            new GetBenchmarkQuery(_currentUser.UserId.Value, _currentUser.OrgId.Value), ct);
        return Ok(result);
    }

    [HttpGet("narratives/history")]
    public async Task<IActionResult> GetNarrativesHistory(
        [FromQuery] int limit = 12,
        CancellationToken ct = default)
    {
        if (_currentUser.UserId == null)
            return Forbid();

        var result = await _mediator.Send(
            new GetNarrativesHistoryQuery(_currentUser.UserId.Value, limit), ct);
        return Ok(result);
    }

    [HttpGet("live-insight")]
    public async Task<IActionResult> GetLiveInsight(CancellationToken ct = default)
    {
        if (_currentUser.UserId == null)
            return Forbid();

        var result = await _mediator.Send(
            new GetLiveInsightQuery(_currentUser.UserId.Value), ct);
        return Ok(result);
    }

    [HttpGet("team-live-insights")]
    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    public async Task<IActionResult> GetTeamLiveInsights(CancellationToken ct = default)
    {
        if (_currentUser.OrgId == null)
            return Forbid();

        var result = await _mediator.Send(
            new GetTeamLiveInsightsQuery(_currentUser.OrgId.Value), ct);
        return Ok(result);
    }

    [HttpGet("reports-insights")]
    public async Task<IActionResult> GetReportsInsights(
        [FromQuery] string? startDate = null,
        [FromQuery] string? endDate = null,
        [FromQuery] Guid? userId = null,
        [FromQuery] bool allTeam = false,
        CancellationToken ct = default)
    {
        if (_currentUser.UserId == null || _currentUser.OrgId == null)
            return Forbid();

        var result = await _mediator.Send(
            new GetReportsInsightsQuery(
                startDate, endDate, userId, allTeam,
                _currentUser.UserId.Value, _currentUser.OrgId.Value), ct);
        return Ok(result);
    }
}
