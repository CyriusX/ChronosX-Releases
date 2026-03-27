using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.Settings;

/// <summary>
/// Handles immediate category update when an admin changes an app's classification.
/// Directly updates the Category_Productivity in SQLite for all matching sessions,
/// so the dashboard and activities reflect the change instantly.
/// </summary>
public sealed class UpdateAppCategoryCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "UpdateAppCategory";

    private readonly IActivitySessionRepository _sessionRepository;
    private readonly ICurrentUserContext _userContext;
    private readonly ILogger<UpdateAppCategoryCommandHandler> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public UpdateAppCategoryCommandHandler(
        IActivitySessionRepository sessionRepository,
        ICurrentUserContext userContext,
        ILogger<UpdateAppCategoryCommandHandler> logger)
    {
        _sessionRepository = sessionRepository;
        _userContext = userContext;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
            UpdateAppCategoryPayload? payload = null;
            if (request.Payload.HasValue && request.Payload.Value.ValueKind == JsonValueKind.Object)
            {
                payload = JsonSerializer.Deserialize<UpdateAppCategoryPayload>(
                    request.Payload.Value.GetRawText(), JsonOptions);
            }

            if (payload == null)
            {
                _logger.LogWarning("UpdateAppCategory: missing payload");
                return SuccessResponse(request.RequestId, new { updated = 0 });
            }

            _logger.LogInformation(
                "UpdateAppCategory: displayName='{DisplayName}', identifier='{Identifier}' → productivity='{Productivity}', subcategory='{Subcategory}'",
                payload.DisplayName, payload.Identifier, payload.Productivity, payload.Subcategory);

            var totalUpdated = 0;

            // Try matching by the resolver's display name (e.g., "VS Code")
            if (!string.IsNullOrEmpty(payload.DisplayName))
            {
                totalUpdated += await _sessionRepository.UpdateCategoryByDisplayNameAsync(
                    payload.DisplayName, payload.Productivity, payload.Subcategory, "org_override", ct);
            }

            // Also try matching by identifier without .exe (e.g., "Code")
            // because Windows may report the app name differently
            if (!string.IsNullOrEmpty(payload.Identifier))
            {
                var exeName = payload.Identifier.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);
                if (!string.IsNullOrEmpty(exeName) && !string.Equals(exeName, payload.DisplayName, StringComparison.OrdinalIgnoreCase))
                {
                    // Try the exe name without extension (e.g., "Code" for code.exe)
                    totalUpdated += await _sessionRepository.UpdateCategoryByDisplayNameAsync(
                        exeName, payload.Productivity, payload.Subcategory, "org_override", ct);
                }
            }

            // If still nothing matched, try a LIKE search for common patterns
            // e.g., "Visual Studio Code" contains "Code" from identifier "code.exe"
            if (totalUpdated == 0 && !string.IsNullOrEmpty(payload.Identifier))
            {
                var baseName = payload.Identifier.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);
                if (!string.IsNullOrEmpty(baseName) && baseName.Length > 2)
                {
                    totalUpdated += await UpdateByPartialMatchAsync(
                        baseName, payload.Productivity, payload.Subcategory, ct);
                }
            }

            _logger.LogInformation("UpdateAppCategory: updated {Count} sessions total", totalUpdated);

            return SuccessResponse(request.RequestId, new { updated = totalUpdated });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateAppCategory failed");
            return SuccessResponse(request.RequestId, new { updated = 0, error = ex.Message });
        }
    }

    private async Task<int> UpdateByPartialMatchAsync(
        string baseName, string productivity, string subcategory, CancellationToken ct)
    {
        var userId = _userContext.UserId;
        if (!userId.HasValue) return 0;

        // Get today's sessions and find those whose DisplayName contains the base name
        var sessions = await _sessionRepository.GetByDateAsync(userId.Value, DateTime.Today, ct);
        var matching = sessions
            .Where(s => s.App.DisplayName.Contains(baseName, StringComparison.OrdinalIgnoreCase))
            .Select(s => s.App.DisplayName)
            .Distinct()
            .ToList();

        var totalUpdated = 0;
        foreach (var displayName in matching)
        {
            _logger.LogInformation("UpdateAppCategory: partial match '{DisplayName}' contains '{BaseName}'",
                displayName, baseName);
            totalUpdated += await _sessionRepository.UpdateCategoryByDisplayNameAsync(
                displayName, productivity, subcategory, "org_override", ct);
        }

        return totalUpdated;
    }

    private sealed class UpdateAppCategoryPayload
    {
        public string DisplayName { get; set; } = string.Empty;
        public string Identifier { get; set; } = string.Empty;
        public string Productivity { get; set; } = string.Empty;
        public string Subcategory { get; set; } = string.Empty;
    }
}
