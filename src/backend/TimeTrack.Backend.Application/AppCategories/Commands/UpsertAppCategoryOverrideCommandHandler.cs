using MediatR;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Application.AppCategories.DTOs;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.AppCategories.Commands;

/// <summary>
/// Handler for UpsertAppCategoryOverrideCommand
///
/// SRP: Apenas processa o comando de upsert
/// DIP: Depende de abstrações (repositórios e contexto)
/// </summary>
public sealed class UpsertAppCategoryOverrideCommandHandler
    : IRequestHandler<UpsertAppCategoryOverrideCommand, AppCategoryOverrideResponse>
{
    private readonly IAppCategoryOverrideRepository _overrideRepo;
    private readonly IActivitySessionRepository _sessionRepo;
    private readonly ICurrentUserContext _currentUser;
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<UpsertAppCategoryOverrideCommandHandler> _logger;

    public UpsertAppCategoryOverrideCommandHandler(
        IAppCategoryOverrideRepository overrideRepo,
        IActivitySessionRepository sessionRepo,
        ICurrentUserContext currentUser,
        IAuditLogService auditLog,
        ILogger<UpsertAppCategoryOverrideCommandHandler> logger)
    {
        _overrideRepo = overrideRepo;
        _sessionRepo = sessionRepo;
        _currentUser = currentUser;
        _auditLog = auditLog;
        _logger = logger;
    }

    public async Task<AppCategoryOverrideResponse> Handle(
        UpsertAppCategoryOverrideCommand command,
        CancellationToken cancellationToken)
    {
        var orgId = command.OrgId;
        var request = command.Request;

        // Validate org access
        if (_currentUser.OrgId != orgId)
        {
            throw new UnauthorizedAccessException("User does not have access to this organization");
        }

        // Only Admin can create/update overrides
        if (_currentUser.Role != UserRole.Admin)
        {
            throw new UnauthorizedAccessException("Only Admins can manage category overrides");
        }

        // Parse and validate values
        var identifierType = ParseIdentifierType(request.IdentifierType);
        var productivity = ParseProductivity(request.Productivity);
        var subcategory = ParseSubcategory(request.Subcategory);

        // Check if override already exists
        var existingOverride = await _overrideRepo.FindAsync(
            orgId,
            request.Identifier,
            cancellationToken);

        AppCategoryOverride categoryOverride;

        if (existingOverride != null)
        {
            // Update existing
            existingOverride.Update(
                productivity,
                subcategory,
                request.DisplayName,
                request.Note);

            await _overrideRepo.UpdateAsync(existingOverride, cancellationToken);
            categoryOverride = existingOverride;

            _logger.LogInformation(
                "Updated category override for {Identifier} in org {OrgId}",
                request.Identifier,
                orgId);
        }
        else
        {
            // Create new
            categoryOverride = AppCategoryOverride.Create(
                orgId,
                request.Identifier,
                identifierType,
                productivity,
                subcategory,
                _currentUser.UserId!.Value,
                request.DisplayName,
                request.Note);

            await _overrideRepo.AddAsync(categoryOverride, cancellationToken);

            _logger.LogInformation(
                "Created category override for {Identifier} in org {OrgId}",
                request.Identifier,
                orgId);
        }

        // Retroactively update all historical sessions in Postgres for this org.
        // The raw identifier from the request is the ProcessName (lowercased) from usage stats.
        // We also try the display name and the base exe name to maximize match coverage.
        var newCategoryStr = productivity switch
        {
            AppProductivityCategory.Productive => "productive",
            AppProductivityCategory.Distraction => "distraction",
            _ => "neutral"
        };
        var newSubcategoryStr = MapSubcategoryToString(subcategory);

        try
        {
            var totalUpdated = 0;

            // Match 1: by raw identifier (which is the ProcessName from usage stats, e.g., "visual studio code")
            totalUpdated += await _sessionRepo.UpdateCategoryByProcessNameAsync(
                orgId, request.Identifier, newCategoryStr, newSubcategoryStr, cancellationToken);

            // Match 2: by display name if different from identifier
            if (!string.IsNullOrEmpty(request.DisplayName) &&
                !string.Equals(request.DisplayName, request.Identifier, StringComparison.OrdinalIgnoreCase))
            {
                totalUpdated += await _sessionRepo.UpdateCategoryByProcessNameAsync(
                    orgId, request.DisplayName, newCategoryStr, newSubcategoryStr, cancellationToken);
            }

            _logger.LogInformation(
                "Retroactively updated {Count} historical sessions for '{Identifier}' in org {OrgId} to {Category}",
                totalUpdated, request.Identifier, orgId, newCategoryStr);
        }
        catch (Exception ex)
        {
            // Non-critical — override is saved, historical update is best-effort
            _logger.LogWarning(ex,
                "Failed to retroactively update historical sessions for '{Identifier}' in org {OrgId}",
                request.Identifier, orgId);
        }

        // Audit log (fire-and-forget)
        _auditLog.LogAsync(
            existingOverride != null ? "category_override.updated" : "category_override.created",
            "app_category_override",
            categoryOverride.Id,
            new
            {
                identifier = categoryOverride.Identifier,
                productivity = categoryOverride.Productivity.ToString(),
                subcategory = categoryOverride.Subcategory.ToString(),
                note = categoryOverride.Note
            },
            cancellationToken);

        return MapToResponse(categoryOverride);
    }

    private static AppIdentifierType ParseIdentifierType(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "exe" => AppIdentifierType.Exe,
            "domain" => AppIdentifierType.Domain,
            _ => throw new ArgumentException($"Invalid identifier type: {value}")
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

    private static AppSubcategory ParseSubcategory(string value)
    {
        // Convert snake_case to PascalCase
        var pascalCase = ToPascalCase(value);

        if (Enum.TryParse<AppSubcategory>(pascalCase, ignoreCase: true, out var result))
        {
            return result;
        }

        throw new ArgumentException($"Invalid subcategory: {value}");
    }

    private static string ToPascalCase(string snakeCase)
    {
        if (string.IsNullOrEmpty(snakeCase))
            return snakeCase;

        var parts = snakeCase.Split('_');
        return string.Concat(parts.Select(p =>
            char.ToUpperInvariant(p[0]) + (p.Length > 1 ? p.Substring(1).ToLowerInvariant() : "")));
    }

    private AppCategoryOverrideResponse MapToResponse(AppCategoryOverride categoryOverride)
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
            CreatedByName = "System", // Would need IUserRepository to get name
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
