using MediatR;

namespace TimeTrack.Backend.Application.AppCategories.Commands;

/// <summary>
/// Command to delete an app category override
///
/// SRP: Apenas representa a intenção de deletar um override
/// CQRS: Command - altera estado
/// </summary>
public sealed record DeleteAppCategoryOverrideCommand : IRequest<bool>
{
    public Guid OrgId { get; init; }
    public string Identifier { get; init; } = null!;

    public DeleteAppCategoryOverrideCommand(Guid orgId, string identifier)
    {
        OrgId = orgId;
        Identifier = identifier;
    }
}
