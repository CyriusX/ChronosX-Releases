using MediatR;
using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Application.Insights.DTOs;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Api.Insights.Queries;

public sealed record GetPatternsQuery(Guid UserId) : IRequest<PatternListResponse>;

public sealed class GetPatternsQueryHandler
    : IRequestHandler<GetPatternsQuery, PatternListResponse>
{
    private readonly TimeTrackDbContext _context;

    public GetPatternsQueryHandler(TimeTrackDbContext context)
    {
        _context = context;
    }

    public async Task<PatternListResponse> Handle(GetPatternsQuery request, CancellationToken ct)
    {
        var patterns = await _context.UserPatterns
            .IgnoreQueryFilters()
            .Where(p => p.UserId == request.UserId && p.IsActive)
            .OrderByDescending(p => p.Strength)
            .Select(p => new PatternItem
            {
                Id = p.Id,
                PatternTag = p.PatternTag,
                Strength = p.Strength,
                Description = p.Description,
                DetectedAt = p.DetectedAt,
            })
            .ToListAsync(ct);

        return new PatternListResponse { Patterns = patterns };
    }
}
