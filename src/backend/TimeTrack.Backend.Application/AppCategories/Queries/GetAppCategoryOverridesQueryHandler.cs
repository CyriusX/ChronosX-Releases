using MediatR;
using TimeTrack.Backend.Application.AppCategories.DTOs;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.AppCategories.Queries;

/// <summary>
/// Handler for GetAppCategoryOverridesQuery
///
/// SRP: Apenas processa a query de listagem de overrides
/// </summary>
public sealed class GetAppCategoryOverridesQueryHandler
    : IRequestHandler<GetAppCategoryOverridesQuery, AppCategoryOverrideListResponse>
{
    private readonly IAppCategoryOverrideRepository _overrideRepo;
    private readonly ICurrentUserContext _currentUser;

    public GetAppCategoryOverridesQueryHandler(
        IAppCategoryOverrideRepository overrideRepo,
        ICurrentUserContext currentUser)
    {
        _overrideRepo = overrideRepo;
        _currentUser = currentUser;
    }

    public async Task<AppCategoryOverrideListResponse> Handle(
        GetAppCategoryOverridesQuery query,
        CancellationToken cancellationToken)
    {
        // Validate org access
        if (_currentUser.OrgId != query.OrgId)
        {
            throw new UnauthorizedAccessException("User does not have access to this organization");
        }

        // Only Admin/Gestor can view overrides
        if (_currentUser.Role != UserRole.Admin && _currentUser.Role != UserRole.Gestor)
        {
            throw new UnauthorizedAccessException("Only Admins and Managers can view category overrides");
        }

        var overrides = await _overrideRepo.GetByOrgIdAsync(query.OrgId, cancellationToken);

        // Apply filter if specified
        if (!string.IsNullOrEmpty(query.ProductivityFilter))
        {
            var productivity = ParseProductivity(query.ProductivityFilter);
            overrides = overrides.Where(o => o.Productivity == productivity).ToList();
        }

        var totalCount = overrides.Count;

        // Apply pagination
        var paged = overrides
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        var responses = paged.Select(MapToResponse).ToList();

        return new AppCategoryOverrideListResponse
        {
            Overrides = responses,
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
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

    private static AppCategoryOverrideResponse MapToResponse(Domain.Entities.AppCategoryOverride categoryOverride)
    {
        return new AppCategoryOverrideResponse
        {
            Id = categoryOverride.Id,
            OrgId = categoryOverride.OrgId,
            Identifier = categoryOverride.Identifier,
            IdentifierType = categoryOverride.IdentifierType.ToString().ToLowerInvariant(),
            DisplayName = categoryOverride.DisplayName ?? FormatDisplayName(categoryOverride.Identifier),
            Productivity = categoryOverride.Productivity.ToString().ToLowerInvariant(),
            Subcategory = MapSubcategoryToString(categoryOverride.Subcategory),
            Source = "org_override",
            Note = categoryOverride.Note,
            CreatedBy = categoryOverride.CreatedBy,
            CreatedByName = "", // Would need to join with users table
            CreatedAt = categoryOverride.CreatedAt,
            UpdatedAt = categoryOverride.UpdatedAt
        };
    }

    private static string FormatDisplayName(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return "Unknown";

        var name = identifier.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);
        if (name.Length > 0)
        {
            name = char.ToUpperInvariant(name[0]) + name.Substring(1);
        }
        name = name.Replace('.', ' ').Replace('-', ' ');
        return name;
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
