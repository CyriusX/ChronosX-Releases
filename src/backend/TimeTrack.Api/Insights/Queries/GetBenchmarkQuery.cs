using MediatR;
using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Application.Insights.DTOs;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Api.Insights.Queries;

public sealed record GetBenchmarkQuery(Guid UserId, Guid OrgId) : IRequest<BenchmarkResponse>;

public sealed class GetBenchmarkQueryHandler
    : IRequestHandler<GetBenchmarkQuery, BenchmarkResponse>
{
    private readonly TimeTrackDbContext _context;

    public GetBenchmarkQueryHandler(TimeTrackDbContext context)
    {
        _context = context;
    }

    public async Task<BenchmarkResponse> Handle(GetBenchmarkQuery request, CancellationToken ct)
    {
        var fourWeeksAgo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-28));

        var userAvg = await _context.FeatureWeeklies
            .IgnoreQueryFilters()
            .Where(w => w.UserId == request.UserId && w.WeekStart >= fourWeeksAgo)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                AvgFocusScore = g.Average(x => x.AvgFocusScore),
                AvgProductiveRatio = g.Average(x => x.AvgProductiveRatio),
            })
            .FirstOrDefaultAsync(ct);

        var orgAvg = await _context.FeatureWeeklies
            .IgnoreQueryFilters()
            .Where(w => w.OrgId == request.OrgId
                && w.UserId != request.UserId
                && w.WeekStart >= fourWeeksAgo)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                AvgFocusScore = g.Average(x => x.AvgFocusScore),
                AvgProductiveRatio = g.Average(x => x.AvgProductiveRatio),
                UserCount = g.Select(x => x.UserId).Distinct().Count(),
            })
            .FirstOrDefaultAsync(ct);

        if (userAvg == null)
            return new BenchmarkResponse { HasData = false };

        var orgFocus = orgAvg?.AvgFocusScore ?? 0;
        var userFocus = userAvg.AvgFocusScore;

        return new BenchmarkResponse
        {
            HasData = true,
            User = new BenchmarkUser
            {
                AvgFocusScore = Math.Round(userFocus, 1),
                AvgProductiveRatio = Math.Round(userAvg.AvgProductiveRatio, 3),
            },
            Team = new BenchmarkTeam
            {
                AvgFocusScore = Math.Round(orgFocus, 1),
                AvgProductiveRatio = Math.Round(orgAvg?.AvgProductiveRatio ?? 0, 3),
                UserCount = orgAvg?.UserCount ?? 0,
            },
            FocusDiff = Math.Round(userFocus - orgFocus, 1),
            FocusDiffPercent = orgFocus > 0 ? Math.Round((userFocus - orgFocus) / orgFocus * 100, 1) : 0,
        };
    }
}
