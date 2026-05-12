using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimeTrack.Api.Security;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Api.Controllers;

[ApiController]
[Route("api/v1/ai")]
[Authorize]
public sealed class AiFeedbackController : ControllerBase
{
    private readonly TimeTrackDbContext _context;
    private readonly ICurrentUserContext _currentUser;

    public AiFeedbackController(
        TimeTrackDbContext context,
        ICurrentUserContext currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Submit feedback for an AI decision (classification review, narrative thumbs up/down, etc.)
    /// </summary>
    [HttpPost("feedback")]
    public async Task<IActionResult> SubmitFeedback(
        [FromBody] FeedbackRequest request,
        CancellationToken ct = default)
    {
        if (_currentUser.OrgId == null)
            return Forbid();

        var validOutcomes = new[] { "accepted", "rejected", "corrected", "useful", "not_useful" };
        if (string.IsNullOrEmpty(request.Outcome) || !validOutcomes.Contains(request.Outcome))
            return BadRequest("Invalid outcome. Must be one of: accepted, rejected, corrected, useful, not_useful");

        var decision = await _context.AiDecisionLogs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == request.DecisionId
                && d.OrgId == _currentUser.OrgId.Value, ct);

        if (decision == null)
            return NotFound();

        JsonElement? correctValue = null;
        if (request.CorrectValue != null)
        {
            correctValue = JsonSerializer.Deserialize<JsonElement>(
                JsonSerializer.Serialize(request.CorrectValue));
        }

        decision.MarkReviewed(request.Outcome, correctValue);
        await _context.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>
    /// Weekly feedback aggregation report (Admin only)
    /// </summary>
    [HttpGet("feedback-report")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> GetFeedbackReport(
        [FromQuery] int weeks = 4,
        CancellationToken ct = default)
    {
        if (_currentUser.OrgId == null)
            return Forbid();

        var since = DateTime.UtcNow.AddDays(-weeks * 7);

        var decisions = await _context.AiDecisionLogs
            .IgnoreQueryFilters()
            .Where(d => d.OrgId == _currentUser.OrgId.Value
                && d.WasReviewed
                && d.CreatedAt >= since)
            .GroupBy(d => new { d.DecisionType, d.ReviewOutcome })
            .Select(g => new
            {
                DecisionType = g.Key.DecisionType,
                Outcome = g.Key.ReviewOutcome,
                Count = g.Count(),
            })
            .ToListAsync(ct);

        var totalByType = decisions
            .GroupBy(d => d.DecisionType)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Count));

        var report = new List<object>();
        foreach (var typeGroup in decisions.GroupBy(d => d.DecisionType))
        {
            var total = totalByType.GetValueOrDefault(typeGroup.Key, 0);
            report.Add(new
            {
                decisionType = typeGroup.Key,
                total,
                breakdown = typeGroup.Select(x => new { outcome = x.Outcome, count = x.Count }),
            });
        }

        return Ok(report);
    }
}

public sealed class FeedbackRequest
{
    public Guid DecisionId { get; init; }
    public string Outcome { get; init; } = string.Empty;
    public object? CorrectValue { get; init; }
}
