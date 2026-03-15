using MediatR;
using TimeTrack.Backend.Application.AppCategories.DTOs;

namespace TimeTrack.Backend.Application.AppCategories.Queries;

/// <summary>
/// Query to list all overrides for an organization
///
/// SRP: Apenas representa a intenção de listar overrides
/// </summary>
public sealed record GetAppCategoryOverridesQuery : IRequest<AppCategoryOverrideListResponse>
{
    public Guid OrgId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public string? ProductivityFilter { get; init; } // "productive" | "neutral" | "distraction"

    public GetAppCategoryOverridesQuery(
        Guid orgId,
        int page = 1,
        int pageSize = 50,
        string? productivityFilter = null)
    {
        OrgId = orgId;
        Page = Math.Max(1, page);
        PageSize = Math.Min(100, Math.Max(1, pageSize));
        ProductivityFilter = productivityFilter;
    }
}
