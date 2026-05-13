using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Application.Insights.DTOs;
using TimeTrack.Backend.Application.Insights.Services;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Api.Insights.Queries;

public sealed record GetWeeklyNarrativeQuery(string? WeekStart, Guid UserId) : IRequest<WeeklyNarrativeResponse>;

public sealed class GetWeeklyNarrativeQueryHandler
    : IRequestHandler<GetWeeklyNarrativeQuery, WeeklyNarrativeResponse>
{
    private readonly TimeTrackDbContext _context;

    public GetWeeklyNarrativeQueryHandler(TimeTrackDbContext context)
    {
        _context = context;
    }

    public async Task<WeeklyNarrativeResponse> Handle(GetWeeklyNarrativeQuery request, CancellationToken ct)
    {
        var weekStartDate = !string.IsNullOrEmpty(request.WeekStart) && DateOnly.TryParse(request.WeekStart, out var ws)
            ? ws
            : InsightTextService.GetWeekStart(DateOnly.FromDateTime(DateTime.UtcNow));

        var weekEndDate = weekStartDate.AddDays(6);
        var weekStartUtc = weekStartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var weekEndUtc = weekEndDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var narrative = await _context.AiDecisionLogs
            .IgnoreQueryFilters()
            .Where(d => d.DecisionType == "weekly_narrative"
                && d.UserId == request.UserId
                && d.CreatedAt >= weekStartUtc
                && d.CreatedAt <= weekEndUtc)
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (narrative == null)
            return new WeeklyNarrativeResponse { WeekStart = weekStartDate.ToString("yyyy-MM-dd") };

        var text = narrative.Output.ValueKind == JsonValueKind.String
            ? narrative.Output.GetString()
            : narrative.Output.GetRawText();

        return new WeeklyNarrativeResponse
        {
            Narrative = text,
            WeekStart = weekStartDate.ToString("yyyy-MM-dd"),
            ModelVersion = narrative.ModelVersion,
            WasReviewed = narrative.WasReviewed,
            ReviewOutcome = narrative.ReviewOutcome,
        };
    }
}
