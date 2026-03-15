using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for global app categories
///
/// SRP: Apenas implementa operações de dados para categorias globais
/// DIP: Implementa interface definida no Domain
/// </summary>
public sealed class AppCategoryGlobalRepository : IAppCategoryGlobalRepository
{
    private readonly TimeTrackDbContext _context;

    public AppCategoryGlobalRepository(TimeTrackDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<AppCategoryGlobal?> FindByIdentifierAsync(
        string identifier,
        CancellationToken cancellationToken = default)
    {
        var normalizedIdentifier = NormalizeIdentifier(identifier);
        return await _context.AppCategoryGlobals
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Identifier == normalizedIdentifier, cancellationToken);
    }

    public async Task<IReadOnlyList<AppCategoryGlobal>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.AppCategoryGlobals
            .AsNoTracking()
            .OrderBy(c => c.DisplayName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AppCategoryGlobal>> SearchAsync(
        string searchTerm,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var normalizedSearch = searchTerm.Trim().ToLowerInvariant();

        return await _context.AppCategoryGlobals
            .AsNoTracking()
            .Where(c =>
                c.DisplayName.ToLower().Contains(normalizedSearch) ||
                c.Identifier.ToLower().Contains(normalizedSearch))
            .OrderBy(c => c.DisplayName)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AppCategoryGlobal>> GetByProductivityAsync(
        AppProductivityCategory productivity,
        CancellationToken cancellationToken = default)
    {
        return await _context.AppCategoryGlobals
            .AsNoTracking()
            .Where(c => c.Productivity == productivity)
            .OrderBy(c => c.DisplayName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AppCategoryGlobal>> GetByIdentifiersAsync(
        IEnumerable<string> identifiers,
        CancellationToken cancellationToken = default)
    {
        var normalizedIdentifiers = identifiers
            .Select(NormalizeIdentifier)
            .ToHashSet();

        return await _context.AppCategoryGlobals
            .AsNoTracking()
            .Where(c => normalizedIdentifiers.Contains(c.Identifier))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(
        string identifier,
        CancellationToken cancellationToken = default)
    {
        var normalizedIdentifier = NormalizeIdentifier(identifier);
        return await _context.AppCategoryGlobals
            .AnyAsync(c => c.Identifier == normalizedIdentifier, cancellationToken);
    }

    public async Task<AppCategoryGlobal> AddAsync(
        AppCategoryGlobal category,
        CancellationToken cancellationToken = default)
    {
        _context.AppCategoryGlobals.Add(category);
        await _context.SaveChangesAsync(cancellationToken);
        return category;
    }

    public async Task AddRangeAsync(
        IEnumerable<AppCategoryGlobal> categories,
        CancellationToken cancellationToken = default)
    {
        await _context.AppCategoryGlobals.AddRangeAsync(categories, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CountAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.AppCategoryGlobals.CountAsync(cancellationToken);
    }

    private static string NormalizeIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return string.Empty;

        var normalized = identifier.Trim().ToLowerInvariant();

        // Add .exe if it's an exe identifier without extension
        if (!normalized.Contains('.') || normalized.EndsWith(".exe"))
        {
            if (!normalized.EndsWith(".exe") && !normalized.Contains('/'))
            {
                // Could be an exe name without extension
                // We'll try both with and without .exe in the query
            }
        }

        return normalized;
    }
}
