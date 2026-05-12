using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Application.Insights.DTOs;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Api.Insights.Queries;

public sealed record GetNarrativesHistoryQuery(Guid UserId, int Limit = 12) : IRequest<NarrativeHistoryResponse>;

public sealed class GetNarrativesHistoryQueryHandler
    : IRequestHandler<GetNarrativesHistoryQuery, NarrativeHistoryResponse>
{
    private readonly TimeTrackDbContext _context;

    public GetNarrativesHistoryQueryHandler(TimeTrackDbContext context)
    {
        _context = context;
    }

    public async Task<NarrativeHistoryResponse> Handle(GetNarrativesHistoryQuery request, CancellationToken ct)
    {
        var raw = await _context.AiDecisionLogs
            .IgnoreQueryFilters()
            .Where(d => d.DecisionType == "weekly_narrative" && d.UserId == request.UserId)
            .OrderByDescending(d => d.CreatedAt)
            .Take(request.Limit)
            .Select(d => new { d.Id, d.Output, d.CreatedAt, d.WasReviewed, d.ReviewOutcome })
            .ToListAsync(ct);

        var narratives = raw.Select(d => new NarrativeHistoryItem
        {
            Id = d.Id,
            Narrative = d.Output.ValueKind == JsonValueKind.String
                ? d.Output.GetString()
                : d.Output.GetRawText(),
            CreatedAt = d.CreatedAt,
            WasReviewed = d.WasReviewed,
            ReviewOutcome = d.ReviewOutcome,
        }).ToList();

        return new NarrativeHistoryResponse { Narratives = narratives };
    }
}
