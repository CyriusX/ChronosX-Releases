using MediatR;
using TimeTrack.Backend.Application.AppCategories.DTOs;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.AppCategories.Queries;

/// <summary>
/// Handler for SearchGlobalCategoriesQuery
///
/// SRP: Apenas processa a query de busca em categorias globais
/// </summary>
public sealed class SearchGlobalCategoriesQueryHandler
    : IRequestHandler<SearchGlobalCategoriesQuery, GlobalCategorySearchResponse>
{
    private readonly IAppCategoryGlobalRepository _globalRepo;
    private readonly IAppCategoryOverrideRepository _overrideRepo;
    private readonly ICurrentUserContext _currentUser;

    public SearchGlobalCategoriesQueryHandler(
        IAppCategoryGlobalRepository globalRepo,
        IAppCategoryOverrideRepository overrideRepo,
        ICurrentUserContext currentUser)
    {
        _globalRepo = globalRepo;
        _overrideRepo = overrideRepo;
        _currentUser = currentUser;
    }

    public async Task<GlobalCategorySearchResponse> Handle(
        SearchGlobalCategoriesQuery query,
        CancellationToken cancellationToken)
    {
        // Validate org access
        if (_currentUser.OrgId != query.OrgId)
        {
            throw new UnauthorizedAccessException("User does not have access to this organization");
        }

        // Get globals
        var globals = string.IsNullOrWhiteSpace(query.SearchTerm)
            ? await _globalRepo.GetAllAsync(cancellationToken)
            : await _globalRepo.SearchAsync(query.SearchTerm, query.Limit, cancellationToken);

        // Apply productivity filter if specified
        if (!string.IsNullOrEmpty(query.ProductivityFilter))
        {
            var productivity = ParseProductivity(query.ProductivityFilter);
            globals = globals.Where(g => g.Productivity == productivity).ToList();
        }

        // Get existing overrides for this org to mark which ones already have overrides
        var overrides = await _overrideRepo.GetByOrgIdAsync(query.OrgId, cancellationToken);
        var overrideIdentifiers = overrides.Select(o => o.Identifier).ToHashSet();

        var items = globals
            .Take(query.Limit)
            .Select(g => new GlobalCategoryItem
            {
                Identifier = g.Identifier,
                IdentifierType = g.IdentifierType.ToString().ToLowerInvariant(),
                DisplayName = g.DisplayName,
                Productivity = g.Productivity.ToString().ToLowerInvariant(),
                Subcategory = MapSubcategoryToString(g.Subcategory),
                HasOverride = overrideIdentifiers.Contains(g.Identifier)
            })
            .ToList();

        return new GlobalCategorySearchResponse
        {
            Items = items,
            TotalCount = items.Count
        };
    }

    private static AppProductivityCategory ParseProductivity(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "productive" => AppProductivityCategory.Productive,
            "neutral" => AppProductivityCategory.Neutral,
            "distraction" => AppProductivityCategory.Distraction,
            _ => throw new ArgumentException($"Invalid productivity: {value}")
        };
    }

    private static string MapSubcategoryToString(AppSubcategory subcategory)
    {
        var name = subcategory.ToString();
        return string.Concat(
            name.Select((c, i) =>
                i > 0 && char.IsUpper(c) ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString())
        );
    }
}
