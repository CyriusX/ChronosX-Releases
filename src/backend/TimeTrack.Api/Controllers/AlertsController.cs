using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimeTrack.Api.Security;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Api.Controllers;

[ApiController]
[Route("api/v1/alerts")]
[Authorize]
public sealed class AlertsController : ControllerBase
{
    private readonly TimeTrackDbContext _context;
    private readonly ICurrentUserContext _currentUser;

    public AlertsController(
        TimeTrackDbContext context,
        ICurrentUserContext currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? severity = null,
        [FromQuery] string? alertType = null,
        [FromQuery] bool? unreadOnly = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (_currentUser.UserId == null || _currentUser.OrgId == null)
            return Forbid();

        var query = _context.SmartAlerts
            .IgnoreQueryFilters()
            .Where(a => a.UserId == _currentUser.UserId.Value
                && a.OrgId == _currentUser.OrgId.Value);

        if (!string.IsNullOrEmpty(severity))
            query = query.Where(a => a.Severity == severity);

        if (!string.IsNullOrEmpty(alertType))
            query = query.Where(a => a.AlertType == alertType);

        if (unreadOnly == true)
            query = query.Where(a => !a.WasRead);

        var total = await query.CountAsync(ct);

        var alerts = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.AlertType,
                a.Message,
                a.Severity,
                a.ActionType,
                a.WasRead,
                a.WasActed,
                a.AboutUserId,
                a.CreatedAt,
            })
            .ToListAsync(ct);

        return Ok(new { alerts, total, page, pageSize });
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct = default)
    {
        if (_currentUser.UserId == null || _currentUser.OrgId == null)
            return Forbid();

        var count = await _context.SmartAlerts
            .IgnoreQueryFilters()
            .CountAsync(a => a.UserId == _currentUser.UserId.Value
                && a.OrgId == _currentUser.OrgId.Value
                && !a.WasRead, ct);

        return Ok(new { count });
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId == null || _currentUser.OrgId == null)
            return Forbid();

        var alert = await _context.SmartAlerts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == id
                && a.UserId == _currentUser.UserId.Value
                && a.OrgId == _currentUser.OrgId.Value, ct);

        if (alert == null)
            return NotFound();

        alert.MarkRead();
        await _context.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct = default)
    {
        if (_currentUser.UserId == null || _currentUser.OrgId == null)
            return Forbid();

        var unread = await _context.SmartAlerts
            .IgnoreQueryFilters()
            .Where(a => a.UserId == _currentUser.UserId.Value
                && a.OrgId == _currentUser.OrgId.Value
                && !a.WasRead)
            .ToListAsync(ct);

        foreach (var alert in unread)
            alert.MarkRead();

        await _context.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpPost("{id:guid}/act")]
    public async Task<IActionResult> MarkActed(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId == null || _currentUser.OrgId == null)
            return Forbid();

        var alert = await _context.SmartAlerts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == id
                && a.UserId == _currentUser.UserId.Value
                && a.OrgId == _currentUser.OrgId.Value, ct);

        if (alert == null)
            return NotFound();

        alert.MarkRead();
        alert.MarkActed();
        await _context.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Dismiss(Guid id, CancellationToken ct = default)
    {
        if (_currentUser.UserId == null || _currentUser.OrgId == null)
            return Forbid();

        var alert = await _context.SmartAlerts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == id
                && a.UserId == _currentUser.UserId.Value
                && a.OrgId == _currentUser.OrgId.Value, ct);

        if (alert == null)
            return NotFound();

        _context.SmartAlerts.Remove(alert);
        await _context.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpGet("team")]
    [Authorize(Policy = AuthorizationPolicies.ManagerOrAdmin)]
    public async Task<IActionResult> ListTeamAlerts(
        [FromQuery] string? severity = null,
        [FromQuery] string? alertType = null,
        [FromQuery] Guid? aboutUserId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (_currentUser.OrgId == null)
            return Forbid();

        var query = _context.SmartAlerts
            .IgnoreQueryFilters()
            .Where(a => a.OrgId == _currentUser.OrgId.Value
                && a.AlertType != "overwork"
                && a.AlertType != "focus_drop");

        if (!string.IsNullOrEmpty(severity))
            query = query.Where(a => a.Severity == severity);

        if (!string.IsNullOrEmpty(alertType))
            query = query.Where(a => a.AlertType == alertType);

        if (aboutUserId.HasValue)
            query = query.Where(a => a.AboutUserId == aboutUserId.Value);

        var total = await query.CountAsync(ct);

        var alerts = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.UserId,
                a.AboutUserId,
                a.AlertType,
                a.Message,
                a.Severity,
                a.ActionType,
                a.CreatedAt,
            })
            .ToListAsync(ct);

        return Ok(new { alerts, total, page, pageSize });
    }
}
