using MediatR;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.AppCategories.Commands;

/// <summary>
/// Handler for DeleteAppCategoryOverrideCommand
///
/// SRP: Apenas processa o comando de deleção
/// </summary>
public sealed class DeleteAppCategoryOverrideCommandHandler
    : IRequestHandler<DeleteAppCategoryOverrideCommand, bool>
{
    private readonly IAppCategoryOverrideRepository _overrideRepo;
    private readonly ICurrentUserContext _currentUser;
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<DeleteAppCategoryOverrideCommandHandler> _logger;

    public DeleteAppCategoryOverrideCommandHandler(
        IAppCategoryOverrideRepository overrideRepo,
        ICurrentUserContext currentUser,
        IAuditLogService auditLog,
        ILogger<DeleteAppCategoryOverrideCommandHandler> logger)
    {
        _overrideRepo = overrideRepo;
        _currentUser = currentUser;
        _auditLog = auditLog;
        _logger = logger;
    }

    public async Task<bool> Handle(
        DeleteAppCategoryOverrideCommand command,
        CancellationToken cancellationToken)
    {
        // Validate org access
        if (_currentUser.OrgId != command.OrgId)
        {
            throw new UnauthorizedAccessException("User does not have access to this organization");
        }

        // Only Admin can delete overrides
        if (_currentUser.Role != UserRole.Admin)
        {
            throw new UnauthorizedAccessException("Only Admins can manage category overrides");
        }

        var deleted = await _overrideRepo.DeleteByOrgAndIdentifierAsync(
            command.OrgId,
            command.Identifier,
            cancellationToken);

        if (deleted)
        {
            _logger.LogInformation(
                "Deleted category override for {Identifier} in org {OrgId}",
                command.Identifier,
                command.OrgId);

            // Audit log (fire-and-forget)
            _auditLog.LogAsync(
                "category_override.deleted",
                "app_category_override",
                null,
                new { identifier = command.Identifier, orgId = command.OrgId },
                cancellationToken);
        }
        else
        {
            _logger.LogWarning(
                "Attempted to delete non-existent override for {Identifier} in org {OrgId}",
                command.Identifier,
                command.OrgId);
        }

        return deleted;
    }
}
