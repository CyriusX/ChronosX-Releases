using MediatR;
using TimeTrack.Backend.Application.AppCategories.DTOs;
using TimeTrack.Backend.Application.AppCategories.Services;

namespace TimeTrack.Backend.Application.AppCategories.Queries;

/// <summary>
/// Handler for GetAppCategoriesBatchQuery
///
/// SRP: Apenas processa a query de batch
/// DIP: Delega resolução para AppCategoryResolver
/// </summary>
public sealed class GetAppCategoriesBatchQueryHandler
    : IRequestHandler<GetAppCategoriesBatchQuery, AppCategoryBatchResponse>
{
    private readonly AppCategoryResolver _resolver;

    public GetAppCategoriesBatchQueryHandler(AppCategoryResolver resolver)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    public async Task<AppCategoryBatchResponse> Handle(
        GetAppCategoriesBatchQuery query,
        CancellationToken cancellationToken)
    {
        return await _resolver.GetAllForOrgAsync(query.OrgId, cancellationToken);
    }
}
