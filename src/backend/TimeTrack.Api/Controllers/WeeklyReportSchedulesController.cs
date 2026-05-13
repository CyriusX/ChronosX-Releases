using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimeTrack.Api.Security;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Reports.DTOs;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Infrastructure.Jobs;
using TimeTrack.Backend.Infrastructure.Persistence;
using AiInterfaces = TimeTrack.Backend.AI.Interfaces;

namespace TimeTrack.Api.Controllers;

[ApiController]
[Route("api/v1/weekly-report-schedule")]
[Authorize]
public sealed class WeeklyReportSchedulesController : ControllerBase
{
    private readonly TimeTrackDbContext _context;
    private readonly ICurrentUserContext _currentUser;
    private readonly WeeklyReportDataCollector _dataCollector;
    private readonly AiInterfaces.IAIService _aiService;

    private static readonly string[] DayNames =
        ["Domingo", "Segunda-feira", "Terca-feira", "Quarta-feira", "Quinta-feira", "Sexta-feira", "Sabado"];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public WeeklyReportSchedulesController(
        TimeTrackDbContext context,
        ICurrentUserContext currentUser,
        WeeklyReportDataCollector dataCollector,
        AiInterfaces.IAIService aiService)
    {
        _context = context;
        _currentUser = currentUser;
        _dataCollector = dataCollector;
        _aiService = aiService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        if (_currentUser.UserId == null || _currentUser.OrgId == null)
            return Forbid();

        var schedule = await _context.WeeklyReportSchedules
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.UserId == _currentUser.UserId.Value, ct);

        if (schedule is null)
            return Ok(null as WeeklyReportScheduleResponse);

        return Ok(MapToResponse(schedule));
    }

    [HttpPut]
    public async Task<IActionResult> Upsert(
        [FromBody] WeeklyReportScheduleRequest request,
        CancellationToken ct)
    {
        if (_currentUser.UserId == null || _currentUser.OrgId == null)
            return Forbid();

        if (request.DayOfWeek is < 0 or > 6)
            return BadRequest("DayOfWeek must be between 0 (Sunday) and 6 (Saturday).");

        if (!TimeOnly.TryParse(request.TimeOfDay, out var timeOfDay))
            return BadRequest("Invalid TimeOfDay format. Use HH:mm.");

        var preferencesJson = JsonSerializer.Serialize(
            new
            {
                request.Preferences?.IncludeTeamComparison,
                request.Preferences?.IncludeDifficultyAnalysis,
                request.Preferences?.IncludeWeekOverWeek,
                request.Preferences?.IncludeUnproductiveDays
            }, JsonOptions);

        var existing = await _context.WeeklyReportSchedules
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.UserId == _currentUser.UserId.Value, ct);

        if (existing is not null)
        {
            existing.Update(request.DayOfWeek, timeOfDay, preferencesJson);
            if (!request.IsEnabled) existing.Disable();
        }
        else
        {
            var newSchedule = WeeklyReportSchedule.Create(
                _currentUser.UserId.Value,
                _currentUser.OrgId.Value,
                request.DayOfWeek,
                timeOfDay,
                preferencesJson);

            if (!request.IsEnabled) newSchedule.Disable();

            _context.WeeklyReportSchedules.Add(newSchedule);
        }

        await _context.SaveChangesAsync(ct);

        var saved = await _context.WeeklyReportSchedules
            .IgnoreQueryFilters()
            .FirstAsync(s => s.UserId == _currentUser.UserId.Value, ct);

        return Ok(MapToResponse(saved));
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(CancellationToken ct)
    {
        if (_currentUser.UserId == null)
            return Forbid();

        var schedule = await _context.WeeklyReportSchedules
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.UserId == _currentUser.UserId.Value, ct);

        if (schedule is null)
            return NotFound();

        _context.WeeklyReportSchedules.Remove(schedule);
        await _context.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpPost("preview")]
    public async Task<IActionResult> Preview(CancellationToken ct)
    {
        if (_currentUser.UserId == null || _currentUser.OrgId == null)
            return Forbid();

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var weekStart = GetWeekStart(today);
        var weekEnd = weekStart.AddDays(6);

        var preferences = new AiInterfaces.ReportPreferences();

        var schedule = await _context.WeeklyReportSchedules
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.UserId == _currentUser.UserId.Value, ct);

        if (schedule is not null)
        {
            try
            {
                preferences = JsonSerializer.Deserialize<AiInterfaces.ReportPreferences>(
                    schedule.PreferencesJson, JsonOptions) ?? AiInterfaces.ReportPreferences.Default;
            }
            catch { /* use default */ }
        }

        var context = await _dataCollector.CollectAsync(
            _currentUser.UserId.Value,
            _currentUser.OrgId.Value,
            weekStart, weekEnd, preferences, ct);

        if (context.CurrentWeek.TotalActiveHours < 1)
            return Ok(new WeeklyReportPreviewResponse
            {
                HtmlReport = "Dados insuficientes para gerar o relatorio desta semana.",
                Period = context.Period
            });

        var report = await _aiService.GenerateWeeklyEmailReportAsync(
            context, _currentUser.OrgId.Value, _currentUser.UserId.Value, ct);

        return Ok(new WeeklyReportPreviewResponse
        {
            HtmlReport = report,
            Period = context.Period
        });
    }

    private WeeklyReportScheduleResponse MapToResponse(WeeklyReportSchedule schedule)
    {
        var prefs = AiInterfaces.ReportPreferences.Default;
        try
        {
            prefs = JsonSerializer.Deserialize<AiInterfaces.ReportPreferences>(
                schedule.PreferencesJson, JsonOptions) ?? AiInterfaces.ReportPreferences.Default;
        }
        catch { /* use default */ }

        return new WeeklyReportScheduleResponse
        {
            Id = schedule.Id,
            DayOfWeek = schedule.DayOfWeek,
            DayName = DayNames[schedule.DayOfWeek],
            TimeOfDay = schedule.TimeOfDay.ToString("HH:mm"),
            IsEnabled = schedule.IsEnabled,
            Preferences = new ReportPreferencesResponse
            {
                IncludeTeamComparison = prefs.IncludeTeamComparison,
                IncludeDifficultyAnalysis = prefs.IncludeDifficultyAnalysis,
                IncludeWeekOverWeek = prefs.IncludeWeekOverWeek,
                IncludeUnproductiveDays = prefs.IncludeUnproductiveDays
            },
            CreatedAt = schedule.CreatedAt,
            UpdatedAt = schedule.UpdatedAt
        };
    }

    private static DateOnly GetWeekStart(DateOnly date)
    {
        var dayOfWeek = (int)date.DayOfWeek;
        var monday = dayOfWeek == 0 ? -6 : 1 - dayOfWeek;
        return date.AddDays(monday);
    }
}
