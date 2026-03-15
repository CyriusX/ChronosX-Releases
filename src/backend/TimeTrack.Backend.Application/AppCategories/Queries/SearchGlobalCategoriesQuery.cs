using MediatR;
using TimeTrack.Backend.Application.AppCategories.DTOs;

namespace TimeTrack.Backend.Application.AppCategories.Queries;

/// <summary>
/// Query to search global categories (for Admin to choose what to override)
///
/// SRP: Apenas representa a intenção de buscar categorias globais
/// </summary>
public sealed record SearchGlobalCategoriesQuery : IRequest<GlobalCategorySearchResponse>
{
    public Guid OrgId { get; init; }
    public string? SearchTerm { get; init; }
    public string? ProductivityFilter { get; init; }
    public int Limit { get; init; } = 50;

    public SearchGlobalCategoriesQuery(
        Guid orgId,
        string? searchTerm = null,
        string? productivityFilter = null,
        int limit = 50)
    {
        OrgId = orgId;
        SearchTerm = searchTerm;
        ProductivityFilter = productivityFilter;
        Limit = Math.Min(100, Math.Max(1, limit));
    }
}
