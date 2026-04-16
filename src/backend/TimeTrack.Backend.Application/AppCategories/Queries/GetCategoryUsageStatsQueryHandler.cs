using MediatR;
using TimeTrack.Backend.Application.AppCategories.DTOs;
using TimeTrack.Backend.Application.AppCategories.Services;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.AppCategories.Queries;

/// <summary>
/// Handler for GetCategoryUsageStatsQuery
///
/// SRP: Apenas processa a query de estatísticas de uso
/// DIP: Usa interfaces (repositório e resolver)
///
/// Composition Pattern: Combina dados de ActivitySessions com categorias
///
/// CX-143: Estatísticas de uso para Admin categorizar apps
/// </summary>
public sealed class GetCategoryUsageStatsQueryHandler
    : IRequestHandler<GetCategoryUsageStatsQuery, CategoryUsageStatsResponse>
{
    private readonly IActivitySessionRepository _sessionRepo;
    private readonly IAppCategoryOverrideRepository _overrideRepo;
    private readonly AppCategoryResolver _categoryResolver;
    private readonly ICurrentUserContext _currentUser;

    public GetCategoryUsageStatsQueryHandler(
        IActivitySessionRepository sessionRepo,
        IAppCategoryOverrideRepository overrideRepo,
        AppCategoryResolver categoryResolver,
        ICurrentUserContext currentUser)
    {
        _sessionRepo = sessionRepo;
        _overrideRepo = overrideRepo;
        _categoryResolver = categoryResolver;
        _currentUser = currentUser;
    }

    public async Task<CategoryUsageStatsResponse> Handle(
        GetCategoryUsageStatsQuery query,
        CancellationToken cancellationToken)
    {
        // Validate org access
        if (_currentUser.OrgId != query.OrgId)
        {
            throw new UnauthorizedAccessException("User does not have access to this organization");
        }

        // Only Admin/Gestor can view stats
        if (_currentUser.Role != UserRole.Admin &&
            _currentUser.Role != UserRole.Gestor)
        {
            throw new UnauthorizedAccessException("Only Admins and Managers can view category statistics");
        }

        // Get all activity sessions in date range
        var sessions = await _sessionRepo.GetByOrgIdAndDateRangeAsync(
            query.OrgId,
            query.StartDate,
            query.EndDate,
            cancellationToken);

        // Aggregate by process name (ActivitySession has ProcessName directly)
        var aggregated = sessions
            .GroupBy(s => s.ProcessName.ToLowerInvariant())
            .Select(g => new
            {
                ProcessName = g.Key,
                DisplayName = g.First().ProcessName,
                TotalMinutes = (int)g.Sum(s => (s.EndedAt - s.StartedAt).TotalSeconds) / 60,
                SessionCount = g.Count()
            })
            .OrderByDescending(g => g.TotalMinutes)
            .ToList();

        // Get existing overrides for this org
        var overrides = await _overrideRepo.GetByOrgIdAsync(query.OrgId, cancellationToken);
        var overrideSet = overrides.Select(o => o.Identifier.ToLowerInvariant()).ToHashSet();

        // Resolve categories for all unique process names
        var identifierList = aggregated.Select(a => a.ProcessName).Distinct().ToList();
        var categories = await _categoryResolver.ResolveBatchAsync(identifierList, query.OrgId, cancellationToken);
        // Build dictionary keyed by the NORMALIZED identifier (with .exe suffix)
        // so we can look up by normalizing the ProcessName at query time.
        var categoryDict = categories.ToDictionary(c => c.Identifier, c => c);

        // Classify and build response
        var productive = new List<CategoryUsageItem>();
        var neutral = new List<CategoryUsageItem>();
        var distraction = new List<CategoryUsageItem>();
        var uncategorized = new List<UncategorizedAppItem>();
        var categorizedCount = 0;

        foreach (var app in aggregated)
        {
            // Normalize ProcessName using IdentifierNormalizer to ensure consistent lookup
            // with the category resolver (handles paths, domains, and exe names consistently)
            var normalizedKey = IdentifierNormalizer.Normalize(app.ProcessName);

            var category = categoryDict.GetValueOrDefault(normalizedKey) ??
                new AppCategoryResponse
                {
                    Identifier = app.ProcessName,
                    IdentifierType = "exe",
                    DisplayName = app.DisplayName,
                    Productivity = "neutral",
                    Subcategory = "unknown",
                    Source = "default"
                };

            if (category.Source == "default")
            {
                uncategorized.Add(new UncategorizedAppItem
                {
                    Identifier = app.ProcessName,
                    IdentifierType = "exe",
                    TotalMinutes = app.TotalMinutes,
                    SessionCount = app.SessionCount,
                    UserCount = 1 // Would need to count distinct users
                });
            }
            else
            {
                categorizedCount++;
                var item = new CategoryUsageItem
                {
                    Identifier = app.ProcessName,
                    DisplayName = category.DisplayName,
                    Productivity = category.Productivity,
                    Subcategory = category.Subcategory,
                    TotalMinutes = app.TotalMinutes,
                    SessionCount = app.SessionCount,
                    Source = category.Source
                };

                switch (category.Productivity)
                {
                    case "productive":
                        productive.Add(item);
                        break;
                    case "distraction":
                        distraction.Add(item);
                        break;
                    default:
                        neutral.Add(item);
                        break;
                }
            }
        }

        return new CategoryUsageStatsResponse
        {
            TopProductiveApps = productive.Take(query.Limit).ToList(),
            TopNeutralApps = neutral.Take(query.Limit).ToList(),
            TopDistractionApps = distraction.Take(query.Limit).ToList(),
            UncategorizedApps = uncategorized.Take(query.Limit).ToList(),
            TotalAppsUsed = aggregated.Count,
            CategorizedApps = categorizedCount,
            UncategorizedCount = uncategorized.Count
        };
    }
}
