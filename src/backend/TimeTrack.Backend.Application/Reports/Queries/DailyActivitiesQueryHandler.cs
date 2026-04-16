using MediatR;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Common.Security;
using TimeTrack.Backend.Application.Reports.DTOs;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.Reports.Queries;

/// <summary>
/// Handler to get individual activity sessions for a specific date.
/// Used by the agent to display timeline blocks for past days.
/// Applies org-level category overrides so the response reflects
/// the admin's classification even for historical sessions.
/// </summary>
public sealed class DailyActivitiesQueryHandler : IRequestHandler<DailyActivitiesQuery, DailyActivitiesResponse>
{
    private readonly IActivitySessionRepository _sessionRepository;
    private readonly IAppCategoryOverrideRepository _overrideRepository;
    private readonly IUserAuthorizationService _authorizationService;
    private readonly ICurrentUserContext _currentUser;

    public DailyActivitiesQueryHandler(
        IActivitySessionRepository sessionRepository,
        IAppCategoryOverrideRepository overrideRepository,
        IUserAuthorizationService authorizationService,
        ICurrentUserContext currentUser)
    {
        _sessionRepository = sessionRepository;
        _overrideRepository = overrideRepository;
        _authorizationService = authorizationService;
        _currentUser = currentUser;
    }

    public async Task<DailyActivitiesResponse> Handle(
        DailyActivitiesQuery request,
        CancellationToken cancellationToken)
    {
        var targetUserId = request.UserId ?? _currentUser.UserId!.Value;

        _authorizationService.EnsureCanAccessUserData(targetUserId);

        // Compute UTC boundaries from the user's timezone so "March 28" in São Paulo
        // correctly spans 03:00 UTC to 03:00 UTC next day (not UTC midnight to midnight)
        DateTime startOfDay, endOfDay;
        if (!string.IsNullOrEmpty(request.Timezone))
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(request.Timezone);
                var localStart = new DateTime(request.Date.Year, request.Date.Month, request.Date.Day, 0, 0, 0, DateTimeKind.Unspecified);
                var localEnd = localStart.AddDays(1);
                startOfDay = TimeZoneInfo.ConvertTimeToUtc(localStart, tz);
                endOfDay = TimeZoneInfo.ConvertTimeToUtc(localEnd, tz);
            }
            catch
            {
                startOfDay = DateTime.SpecifyKind(request.Date.Date, DateTimeKind.Utc);
                endOfDay = startOfDay.AddDays(1);
            }
        }
        else
        {
            startOfDay = DateTime.SpecifyKind(request.Date.Date, DateTimeKind.Utc);
            endOfDay = startOfDay.AddDays(1);
        }

        var sessions = await _sessionRepository.GetByUserIdAndDateRangeAsync(
            targetUserId, startOfDay, endOfDay, cancellationToken);

        // Load org-level overrides for the user's org
        var overrideLookup = await BuildOverrideLookupAsync(sessions, cancellationToken);

        var sessionDtos = sessions
            .Select(s => new ActivitySessionDto
            {
                ProcessName = s.ProcessName,
                WindowTitle = s.WindowTitle,
                AppCategory = ResolveCategory(s.ProcessName, s.AppCategory, overrideLookup),
                StartedAt = s.StartedAt,
                EndedAt = s.EndedAt,
                DurationSeconds = (int)(s.EndedAt - s.StartedAt).TotalSeconds,
            })
            .ToList();

        return new DailyActivitiesResponse
        {
            Date = request.Date.ToString("yyyy-MM-dd"),
            Sessions = sessionDtos,
        };
    }

    /// <summary>
    /// Builds a lookup from normalized process name to overridden category string.
    /// Gets orgId from the first session (all sessions for a user share the same org).
    /// </summary>
    private async Task<Dictionary<string, string>> BuildOverrideLookupAsync(
        IEnumerable<ActivitySession> sessions, CancellationToken ct)
    {
        try
        {
            var firstSession = sessions.FirstOrDefault();
            if (firstSession == null) return new();

            var overrides = await _overrideRepository.GetByOrgIdAsync(firstSession.OrgId, ct);
            if (overrides.Count == 0) return new();

            // Use last-wins to avoid duplicate key exceptions
            var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var o in overrides)
            {
                var key = o.Identifier?.ToLowerInvariant() ?? "";
                if (string.IsNullOrEmpty(key)) continue;
                lookup[key] = o.Productivity switch
                {
                    AppProductivityCategory.Productive => "productive",
                    AppProductivityCategory.Distraction => "distraction",
                    _ => "neutral"
                };
            }
            return lookup;
        }
        catch
        {
            // Never break activity queries due to override loading failure
            return new();
        }
    }

    /// <summary>
    /// Resolves category: if an override exists for the process, use it; otherwise keep the stored value.
    /// </summary>
    private static string? ResolveCategory(string processName, string? storedCategory, Dictionary<string, string> overrides)
    {
        if (overrides.Count == 0) return storedCategory;

        var normalized = processName.Trim().ToLowerInvariant();
        if (overrides.TryGetValue(normalized, out var ov)) return ov;
        if (normalized.EndsWith(".exe") && overrides.TryGetValue(normalized[..^4], out ov)) return ov;
        if (!normalized.EndsWith(".exe") && overrides.TryGetValue(normalized + ".exe", out ov)) return ov;

        return storedCategory;
    }
}
