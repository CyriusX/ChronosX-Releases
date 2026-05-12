using MediatR;
using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Application.FocusScore;
using TimeTrack.Backend.Application.Insights.DTOs;
using TimeTrack.Backend.Domain.Constants;
using TimeTrack.Backend.Domain.ValueObjects;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Api.Insights.Queries;

public sealed record GetTeamLiveInsightsQuery(Guid OrgId)
    : IRequest<TeamLiveInsightResponse>;

public sealed class GetTeamLiveInsightsQueryHandler
    : IRequestHandler<GetTeamLiveInsightsQuery, TeamLiveInsightResponse>
{
    private readonly TimeTrackDbContext _context;
    private readonly AppProductivityClassifier _classifier;

    public GetTeamLiveInsightsQueryHandler(
        TimeTrackDbContext context,
        AppProductivityClassifier classifier)
    {
        _context = context;
        _classifier = classifier;
    }

    public async Task<TeamLiveInsightResponse> Handle(
        GetTeamLiveInsightsQuery request,
        CancellationToken ct)
    {
        var orgId = request.OrgId;
        var todayStartUtc = DateTime.UtcNow.Date;

        // 1. Load active users in org
        var members = await _context.Users
            .IgnoreQueryFilters()
            .Where(u => u.OrgId == orgId && u.Status == UserStatus.Active)
            .Select(u => new { u.Id, u.DisplayName })
            .ToListAsync(ct);

        // 2. Load today's ActivitySessions grouped by UserId
        var sessionsByUser = await _context.ActivitySessions
            .IgnoreQueryFilters()
            .Where(s => s.OrgId == orgId && s.StartedAt >= todayStartUtc)
            .GroupBy(s => s.UserId)
            .Select(g => new { UserId = g.Key, Sessions = g.ToList() })
            .ToListAsync(ct);

        var memberResults = new List<TeamMemberLiveInsight>();

        // 3. For each member with sessions
        foreach (var member in members)
        {
            var userSessionGroup = sessionsByUser.FirstOrDefault(ms => ms.UserId == member.Id);
            if (userSessionGroup == null || userSessionGroup.Sessions.Count == 0)
                continue;

            // Filter internal apps
            var filteredSessions = userSessionGroup.Sessions
                .Where(s => !InternalApps.IsInternal(s.ProcessName))
                .ToList();

            if (filteredSessions.Count == 0)
                continue;

            long prodMs = 0, distMs = 0, neutralMs = 0;
            int distCount = 0;
            long longestBlock = 0, currentBlock = 0;

            foreach (var s in filteredSessions)
            {
                var cat = _classifier.ClassifyWithContext(s.ProcessName, s.WindowTitle);
                var dur = s.DurationSeconds * 1000L;

                switch (cat)
                {
                    case AppProductivityCategory.Productive:
                        prodMs += dur;
                        currentBlock += dur;
                        break;
                    case AppProductivityCategory.Distraction:
                        distMs += dur;
                        distCount++;
                        if (currentBlock > longestBlock) longestBlock = currentBlock;
                        currentBlock = 0;
                        break;
                    default:
                        neutralMs += dur;
                        break;
                }
            }

            if (currentBlock > longestBlock) longestBlock = currentBlock;

            var totalMs = prodMs + distMs + neutralMs;

            var focusInput = new FocusScoreInput
            {
                TotalTrackedMs = totalMs,
                FocusTimeMs = prodMs,
                DistractionMs = distMs,
                DistractionCount = distCount,
                PauseCount = 0,
                IdleCount = 0,
                LongFocusBlockCount = longestBlock > 1_500_000 ? 1 : 0
            };

            var focusEstimate = FocusScoreCalculator.Calculate(focusInput);

            // Load latest SmartAlert for user with AlertType.StartsWith("live_")
            var latestAlert = await _context.SmartAlerts
                .IgnoreQueryFilters()
                .Where(a => a.UserId == member.Id && a.AlertType.StartsWith("live_"))
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new AlertItem
                {
                    Id = a.Id,
                    AlertType = a.AlertType,
                    Message = a.Message,
                    Severity = a.Severity,
                    ActionType = a.ActionType,
                    CreatedAt = a.CreatedAt
                })
                .FirstOrDefaultAsync(ct);

            memberResults.Add(new TeamMemberLiveInsight
            {
                UserId = member.Id,
                UserName = member.DisplayName,
                FocusEstimate = focusEstimate,
                ActiveSeconds = totalMs / 1000,
                LatestAlert = latestAlert
            });
        }

        // 4. Load org alert where AlertType == "live_team_distraction"
        var orgAlert = await _context.SmartAlerts
            .IgnoreQueryFilters()
            .Where(a => a.OrgId == orgId && a.AlertType == "live_team_distraction")
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new AlertItem
            {
                Id = a.Id,
                AlertType = a.AlertType,
                Message = a.Message,
                Severity = a.Severity,
                ActionType = a.ActionType,
                CreatedAt = a.CreatedAt
            })
            .FirstOrDefaultAsync(ct);

        // 5. Return TeamLiveInsightResponse
        return new TeamLiveInsightResponse
        {
            Members = memberResults,
            OrgAlert = orgAlert
        };
    }
}
