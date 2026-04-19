using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Infrastructure.Utilities;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Queries.Dashboard;

/// <summary>
/// Returns the top accessed folders for a given day (default: today) based on ActivitySession.FilePath.
/// This is used by the DesktopHost dashboard card and is computed locally from SQLite for real-time data.
/// </summary>
public sealed class GetTopFoldersQueryHandler : IpcHandlerBase, IIpcQueryHandler
{
    public string QueryName => "GetTopFolders";

    private static readonly HashSet<string> AllowedApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "File Explorer",
        "Finder"
    };

    private readonly IActivitySessionRepository _sessions;
    private readonly ICurrentUserContext _userContext;
    private readonly ILogger<GetTopFoldersQueryHandler> _logger;

    public GetTopFoldersQueryHandler(
        IActivitySessionRepository sessions,
        ICurrentUserContext userContext,
        ILogger<GetTopFoldersQueryHandler> logger)
    {
        _sessions = sessions;
        _userContext = userContext;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
            if (!_userContext.UserId.HasValue)
            {
                return SuccessResponse(request.RequestId, new { folders = Array.Empty<object>() });
            }

            var (targetLocalDate, limit) = ExtractParams(request);

            // Convert local day boundaries to UTC for clipping.
            var localStart = DateTime.SpecifyKind(targetLocalDate.Date, DateTimeKind.Local);
            var startUtc = localStart.ToUniversalTime();
            var endUtc = localStart.AddDays(1).ToUniversalTime();

            var sessions = await _sessions.GetByDateRangeAsync(_userContext.UserId.Value, startUtc, endUtc, ct);

            var dict = new Dictionary<string, (long TotalSeconds, int VisitCount)>(StringComparer.OrdinalIgnoreCase);

            foreach (var s in sessions)
            {
                if (!AllowedApps.Contains(s.App.DisplayName))
                    continue;

                if (string.IsNullOrWhiteSpace(s.FilePath))
                    continue;

                var folder = FolderKeyNormalizer.Normalize(s.FilePath);
                if (string.IsNullOrWhiteSpace(folder))
                    continue;

                var clipped = ClipSeconds(s.Period.StartUtc, s.Period.EndUtc, startUtc, endUtc);
                if (clipped <= 0)
                    continue;

                if (dict.TryGetValue(folder, out var agg))
                    dict[folder] = (agg.TotalSeconds + clipped, agg.VisitCount + 1);
                else
                    dict[folder] = (clipped, 1);
            }

            var folders = dict
                .Select(kvp => new
                {
                    folderPath = kvp.Key,
                    totalSeconds = kvp.Value.TotalSeconds,
                    visitCount = kvp.Value.VisitCount
                })
                .OrderByDescending(f => f.totalSeconds)
                .Take(Math.Clamp(limit, 1, 50))
                .ToArray();

            return SuccessResponse(request.RequestId, new { folders });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting top folders");
            return UnknownErrorResponse(request.RequestId, ex);
        }
    }

    private static (DateTime TargetLocalDate, int Limit) ExtractParams(IpcRequest request)
    {
        var targetDate = DateTime.Today;
        var limit = 5;

        if (request.Payload.HasValue && request.Payload.Value.ValueKind == JsonValueKind.Object)
        {
            if (request.Payload.Value.TryGetProperty("date", out var dateEl))
            {
                var raw = dateEl.GetString();
                if (!string.IsNullOrWhiteSpace(raw) && DateOnly.TryParse(raw, out var d))
                {
                    targetDate = new DateTime(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Local);
                }
            }

            if (request.Payload.Value.TryGetProperty("limit", out var limitEl))
            {
                if (limitEl.ValueKind == JsonValueKind.Number && limitEl.TryGetInt32(out var l))
                    limit = l;
            }
        }

        return (targetDate, limit);
    }

    private static long ClipSeconds(DateTime start, DateTime end, DateTime rangeStart, DateTime rangeEnd)
    {
        var s = start < rangeStart ? rangeStart : start;
        var e = end > rangeEnd ? rangeEnd : end;
        return (long)Math.Max(0, (e - s).TotalSeconds);
    }

    // Folder normalization is shared with backend logic to keep results consistent across IPC and API.
}
