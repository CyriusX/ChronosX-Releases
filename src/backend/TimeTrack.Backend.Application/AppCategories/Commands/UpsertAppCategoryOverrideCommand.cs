using MediatR;
using TimeTrack.Backend.Application.AppCategories.DTOs;

namespace TimeTrack.Backend.Application.AppCategories.Commands;

/// <summary>
/// Command to create or update an app category override for an organization
///
/// SRP: Apenas representa a intenção de criar/atualizar um override
/// CQRS: Command - altera estado
/// </summary>
public sealed record UpsertAppCategoryOverrideCommand : IRequest<AppCategoryOverrideResponse>
{
    public Guid OrgId { get; init; }
    public UpsertAppCategoryOverrideRequest Request { get; init; } = null!;

    public UpsertAppCategoryOverrideCommand(Guid orgId, UpsertAppCategoryOverrideRequest request)
    {
        OrgId = orgId;
        Request = request;
    }
}
