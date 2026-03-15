using MediatR;
using TimeTrack.Backend.Application.AppCategories.DTOs;

namespace TimeTrack.Backend.Application.AppCategories.Queries;

/// <summary>
/// Query to get category usage statistics for Admin dashboard
/// Shows what apps are being used and which ones need categorization
///
/// SRP: Apenas representa a intenção de buscar estatísticas de uso
/// </summary>
public sealed record GetCategoryUsageStatsQuery : IRequest<CategoryUsageStatsResponse>
{
    public Guid OrgId { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public int Limit { get; init; } = 10; // Top N per category

    public GetCategoryUsageStatsQuery(
        Guid orgId,
        DateTime startDate,
        DateTime endDate,
        int limit = 10)
    {
        OrgId = orgId;
        StartDate = startDate;
        EndDate = endDate;
        Limit = Math.Min(50, Math.Max(1, limit));
    }
}
