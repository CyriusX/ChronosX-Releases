using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.AI.Interfaces;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Jobs;

public sealed class WeeklyEmailReportJob
{
    private readonly TimeTrackDbContext _context;
    private readonly WeeklyReportDataCollector _dataCollector;
    private readonly IAIService _aiService;
    private readonly IEmailService _emailService;
    private readonly ILogger<WeeklyEmailReportJob> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public WeeklyEmailReportJob(
        TimeTrackDbContext context,
        WeeklyReportDataCollector dataCollector,
        IAIService aiService,
        IEmailService emailService,
        ILogger<WeeklyEmailReportJob> logger)
    {
        _context = context;
        _dataCollector = dataCollector;
        _aiService = aiService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var nowUtc = DateTime.UtcNow;
        var currentDayOfWeek = (int)nowUtc.DayOfWeek;
        var currentTime = TimeOnly.FromDateTime(nowUtc);

        // Find schedules matching current day and hour window
        var schedules = await _context.WeeklyReportSchedules
            .IgnoreQueryFilters()
            .Where(s => s.IsEnabled
                && s.DayOfWeek == currentDayOfWeek
                && s.TimeOfDay.Hour == currentTime.Hour)
            .ToListAsync(ct);

        if (schedules.Count == 0) return;

        _logger.LogInformation("WeeklyEmailReportJob: processing {Count} schedules", schedules.Count);

        var today = DateOnly.FromDateTime(nowUtc);
        var weekStart = GetWeekStart(today);
        var weekEnd = weekStart.AddDays(6);

        var sent = 0;
        foreach (var schedule in schedules)
        {
            try
            {
                await ProcessScheduleAsync(schedule, weekStart, weekEnd, ct);
                sent++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending weekly report for user {UserId}", schedule.UserId);
            }
        }

        _logger.LogInformation("WeeklyEmailReportJob completed. Sent: {Sent}/{Total}", sent, schedules.Count);
    }

    private async Task ProcessScheduleAsync(
        WeeklyReportSchedule schedule,
        DateOnly weekStart,
        DateOnly weekEnd,
        CancellationToken ct)
    {
        var preferences = DeserializePreferences(schedule.PreferencesJson);

        var context = await _dataCollector.CollectAsync(
            schedule.UserId, schedule.OrgId,
            weekStart, weekEnd, preferences, ct);

        if (context.CurrentWeek.TotalActiveHours < 1)
        {
            _logger.LogInformation("Skipping report for user {UserId}: insufficient data ({Hours}h)",
                schedule.UserId, context.CurrentWeek.TotalActiveHours);
            return;
        }

        var report = await _aiService.GenerateWeeklyEmailReportAsync(
            context, schedule.OrgId, schedule.UserId, ct);

        var user = await _context.Users
            .IgnoreQueryFilters()
            .Where(u => u.Id == schedule.UserId)
            .Select(u => new { u.Email, u.DisplayName })
            .FirstOrDefaultAsync(ct);

        if (user is null) return;

        await _emailService.SendWeeklyReportEmailAsync(
            user.Email,
            user.DisplayName,
            report,
            context.Period,
            ct);

        _logger.LogInformation("Weekly report email sent to {Email}", user.Email);
    }

    private static ReportPreferences DeserializePreferences(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ReportPreferences>(json, JsonOptions) ?? ReportPreferences.Default;
        }
        catch
        {
            return ReportPreferences.Default;
        }
    }

    private static DateOnly GetWeekStart(DateOnly date)
    {
        var dayOfWeek = (int)date.DayOfWeek;
        var monday = dayOfWeek == 0 ? -6 : 1 - dayOfWeek;
        return date.AddDays(monday);
    }
}
