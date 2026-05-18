using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Repositories;

public sealed class PlatformHealthStateRepository : IPlatformHealthStateRepository
{
    private static readonly Guid SingletonId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly TimeTrackDbContext _context;

    public PlatformHealthStateRepository(TimeTrackDbContext context)
    {
        _context = context;
    }

    public async Task<PlatformHealthState> GetAsync(CancellationToken cancellationToken = default)
    {
        var entity = await _context.PlatformHealthState
            .FirstOrDefaultAsync(e => e.Id == SingletonId, cancellationToken);

        if (entity is not null) return entity;

        // Should never happen because DatabaseInitializer seeds it.
        entity = PlatformHealthState.CreateInitial(SingletonId);
        await _context.PlatformHealthState.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task UpdateAsync(PlatformHealthState entity, CancellationToken cancellationToken = default)
    {
        _context.PlatformHealthState.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

