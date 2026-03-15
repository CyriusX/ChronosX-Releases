using MediatR;
using TimeTrack.Backend.Application.AppCategories.DTOs;

namespace TimeTrack.Backend.Application.AppCategories.Queries;

/// <summary>
/// Query to get all categories for an organization (for Agent sync)
///
/// SRP: Apenas representa a intenção de buscar categorias
/// CQRS: Query - não altera estado
/// </summary>
public sealed record GetAppCategoriesBatchQuery : IRequest<AppCategoryBatchResponse>
{
    public Guid OrgId { get; init; }
    public int? Version { get; init; } // For cache validation (optional)

    public GetAppCategoriesBatchQuery(Guid orgId, int? version = null)
    {
        OrgId = orgId;
        Version = version;
    }
}
