using MediatR;
using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Application.Insights.DTOs;
using TimeTrack.Backend.Application.Insights.Services;
using TimeTrack.Backend.Domain.Constants;
using TimeTrack.Backend.Domain.ValueObjects;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Api.Insights.Queries;

public sealed record GetLiveInsightQuery(Guid UserId) : IRequest<LiveInsightResponse>;

public sealed class GetLiveInsightQueryHandler
    : IRequestHandler<GetLiveInsightQuery, LiveInsightResponse>
{
    private readonly TimeTrackDbContext _context;

    public GetLiveInsightQueryHandler(TimeTrackDbContext context)
    {
        _context = context;
    }

    public async Task<LiveInsightResponse> Handle(GetLiveInsightQuery request, CancellationToken ct)
    {
        var todayStartUtc = DateTime.UtcNow.Date;

        var sessions = await _context.ActivitySessions
            .IgnoreQueryFilters()
            .Where(s => s.UserId == request.UserId && s.StartedAt >= todayStartUtc)
            .OrderBy(s => s.StartedAt)
            .ToListAsync(ct);

        sessions = sessions.Where(s => !InternalApps.IsInternal(s.ProcessName)).ToList();

        if (sessions.Count == 0)
            return new LiveInsightResponse { HasData = false };

        long productiveMs = 0, distractionMs = 0, neutralMs = 0;
        var contextSwitches = 0;
        var longestFocusBlockMs = 0L;
        var currentFocusBlockMs = 0L;
        string? lastProcess = null;
        var distractionByApp = new Dictionary<string, long>();

        foreach (var session in sessions)
        {
            var category = InsightTextService.MapAppCategory(session.AppCategory);
            var durationMs = session.DurationSeconds * 1000L;

            switch (category)
            {
                case AppProductivityCategory.Productive:
                    productiveMs += durationMs;
                    currentFocusBlockMs += durationMs;
                    break;
                case AppProductivityCategory.Distraction:
                    distractionMs += durationMs;
                    if (currentFocusBlockMs > longestFocusBlockMs)
                        longestFocusBlockMs = currentFocusBlockMs;
                    currentFocusBlockMs = 0;
                    if (!string.IsNullOrEmpty(session.ProcessName))
                        distractionByApp[session.ProcessName] = distractionByApp.GetValueOrDefault(session.ProcessName) + durationMs;
                    break;
                default:
                    neutralMs += durationMs;
                    break;
            }

            if (lastProcess != null && lastProcess != session.ProcessName)
                contextSwitches++;
            lastProcess = session.ProcessName;
        }

        if (currentFocusBlockMs > longestFocusBlockMs)
            longestFocusBlockMs = currentFocusBlockMs;

        var insights = InsightTextService.GenerateLiveInsights(
            distractionMs, productiveMs, productiveMs + distractionMs + neutralMs,
            longestFocusBlockMs, contextSwitches, distractionByApp);

        var latestAlert = await _context.SmartAlerts
            .IgnoreQueryFilters()
            .Where(a => a.UserId == request.UserId && a.AlertType.StartsWith("live_"))
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AlertItem { Id = a.Id, AlertType = a.AlertType, Message = a.Message, Severity = a.Severity, CreatedAt = a.CreatedAt })
            .FirstOrDefaultAsync(ct);

        return new LiveInsightResponse
        {
            HasData = true,
            ProductiveSeconds = productiveMs / 1000,
            DistractionSeconds = distractionMs / 1000,
            NeutralSeconds = neutralMs / 1000,
            ContextSwitches = contextSwitches,
            LongestFocusMinutes = longestFocusBlockMs / 60_000,
            Insights = insights,
            LatestAlert = latestAlert,
        };
    }
}
