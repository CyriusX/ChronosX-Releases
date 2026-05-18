using MediatR;
using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Application.Insights.DTOs;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Api.Insights.Queries;

public sealed record GetFocusTrendQuery(Guid UserId) : IRequest<FocusTrendResponse>;

public sealed class GetFocusTrendQueryHandler
    : IRequestHandler<GetFocusTrendQuery, FocusTrendResponse>
{
    private readonly TimeTrackDbContext _context;

    public GetFocusTrendQueryHandler(TimeTrackDbContext context)
    {
        _context = context;
    }

    public async Task<FocusTrendResponse> Handle(GetFocusTrendQuery request, CancellationToken ct)
    {
        var eightWeeksAgo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-56));

        var data = await _context.FeatureWeeklies
            .IgnoreQueryFilters()
            .Where(w => w.UserId == request.UserId && w.WeekStart >= eightWeeksAgo)
            .OrderBy(w => w.WeekStart)
            .Select(w => new FocusTrendDataPoint
            {
                WeekStart = w.WeekStart,
                AvgFocusScore = w.AvgFocusScore,
                AvgProductiveRatio = w.AvgProductiveRatio,
                TotalActiveHours = w.TotalActiveHours,
                TrendFocusScore = w.TrendFocusScore,
            })
            .ToListAsync(ct);

        return new FocusTrendResponse { Data = data };
    }
}
