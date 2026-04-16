using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Application.FocusScore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Repositories;

/// <summary>
/// Implementação de consultas de relatórios otimizadas com GROUP BY
///
/// SRP: Apenas consultas de agregação
/// OCP: Novos métodos podem ser adicionados sem modificar existentes
/// </summary>
public sealed class ReportRepository : IReportRepository
{
    private readonly TimeTrackDbContext _context;
    private readonly ILogger<ReportRepository>? _logger;

    // Use shared constant for consistent filtering across all views
    private static readonly HashSet<string> InternalApps = TimeTrack.Backend.Domain.Constants.InternalApps.ProcessNames;

    // Cache em memória das categorias globais (carregado uma vez por instância)
    private Dictionary<string, AppCategoryGlobal>? _categoryCache;
    private readonly object _cacheLock = new();

    // Override cache per org (loaded once per request lifetime / scoped)
    private readonly Dictionary<Guid, Dictionary<string, (string Productivity, string Subcategory)>> _overrideCache = new();

    public ReportRepository(TimeTrackDbContext context, ILogger<ReportRepository>? logger = null)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Carrega o cache de categorias globais (lazy loading)
    /// </summary>
    private async Task<Dictionary<string, AppCategoryGlobal>> GetCategoryCacheAsync(CancellationToken cancellationToken)
    {
        if (_categoryCache != null)
            return _categoryCache;

        lock (_cacheLock)
        {
            if (_categoryCache != null)
                return _categoryCache;
        }

        var categories = await _context.AppCategoryGlobals
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var cache = categories.ToDictionary(
            c => c.Identifier.ToLowerInvariant(),
            c => c);

        lock (_cacheLock)
        {
            _categoryCache ??= cache;
        }

        return _categoryCache;
    }

    /// <summary>
    /// Loads the org-level category overrides for a given user, keyed by normalized identifier.
    /// Returns a lookup: normalizedIdentifier → (productivity, subcategory).
    /// NEVER throws — returns empty dict on any failure so reports still work.
    /// </summary>
    private async Task<Dictionary<string, (string Productivity, string Subcategory)>> GetOverridesForUserAsync(
        Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            // Get the user's orgId — bypass multi-tenancy filter to ensure we find the user
            var user = await _context.Users
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(u => u.Id == userId)
                .Select(u => new { u.OrgId })
                .FirstOrDefaultAsync(cancellationToken);

            if (user == null)
            {
                _logger?.LogWarning("GetOverridesForUserAsync: User {UserId} not found", userId);
                return new();
            }

            if (_overrideCache.TryGetValue(user.OrgId, out var cached))
                return cached;

            var overrides = await _context.AppCategoryOverrides
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(o => o.OrgId == user.OrgId)
                .Select(o => new { o.Identifier, o.Productivity, o.Subcategory })
                .ToListAsync(cancellationToken);

            // Use last-wins for duplicate identifiers (safe against duplicate key exceptions)
            var lookup = new Dictionary<string, (string Productivity, string Subcategory)>(StringComparer.OrdinalIgnoreCase);
            foreach (var o in overrides)
            {
                var key = o.Identifier?.ToLowerInvariant() ?? "";
                if (string.IsNullOrEmpty(key)) continue;
                lookup[key] = (
                    o.Productivity switch
                    {
                        AppProductivityCategory.Productive => "productive",
                        AppProductivityCategory.Distraction => "distraction",
                        _ => "neutral"
                    },
                    o.Subcategory.ToString().ToLowerInvariant()
                );
            }

            _overrideCache[user.OrgId] = lookup;
            _logger?.LogInformation("Loaded {Count} category overrides for org {OrgId}", lookup.Count, user.OrgId);
            return lookup;
        }
        catch (Exception ex)
        {
            // NEVER let override loading break report queries — fall back to stored categories
            _logger?.LogError(ex, "GetOverridesForUserAsync failed for user {UserId}, falling back to stored categories", userId);
            return new();
        }
    }

    /// <summary>
    /// Resolves productivity considering org-level overrides.
    /// Priority: override (by processName) > stored AppCategory from session.
    /// </summary>
    private string? ResolveProductivityWithOverrides(
        string? processName,
        string? storedAppCategory,
        Dictionary<string, (string Productivity, string Subcategory)> overrides)
    {
        if (!string.IsNullOrEmpty(processName) && overrides.Count > 0)
        {
            var normalized = processName.Trim().ToLowerInvariant();
            if (overrides.TryGetValue(normalized, out var ov))
                return ov.Productivity;
            // Try without .exe suffix (override may have been created without it)
            if (normalized.EndsWith(".exe") && overrides.TryGetValue(normalized[..^4], out ov))
                return ov.Productivity;
            // Try with .exe suffix (override may have been created with it)
            if (!normalized.EndsWith(".exe") && overrides.TryGetValue(normalized + ".exe", out ov))
                return ov.Productivity;
        }
        return ResolveProductivity(storedAppCategory);
    }

    /// <summary>
    /// Resolves subcategory considering org-level overrides.
    /// </summary>
    private string? ResolveSubcategoryWithOverrides(
        string? processName,
        string? storedAppCategory,
        string? storedAppSubcategory,
        Dictionary<string, (string Productivity, string Subcategory)> overrides)
    {
        if (!string.IsNullOrEmpty(processName) && overrides.Count > 0)
        {
            var normalized = processName.Trim().ToLowerInvariant();
            if (overrides.TryGetValue(normalized, out var ov))
                return ov.Subcategory;
            if (normalized.EndsWith(".exe") && overrides.TryGetValue(normalized[..^4], out ov))
                return ov.Subcategory;
            if (!normalized.EndsWith(".exe") && overrides.TryGetValue(normalized + ".exe", out ov))
                return ov.Subcategory;
        }
        return ResolveSubcategory(storedAppCategory, storedAppSubcategory);
    }

    /// <summary>
    /// Ensures DateTime is UTC for PostgreSQL compatibility
    /// </summary>
    private static DateTime EnsureUtc(DateTime dt)
    {
        if (dt.Kind == DateTimeKind.Utc)
            return dt;
        if (dt.Kind == DateTimeKind.Local)
            return dt.ToUniversalTime();
        // Kind == Unspecified: treat as UTC (already in correct format from frontend)
        return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
    }

    /// <summary>
    /// Computes the UTC start/end boundaries for a local date range given the user's IANA timezone.
    /// For example, "2026-03-24" in "America/Sao_Paulo" (UTC-3) maps to
    /// start = 2026-03-24T03:00:00Z, end = 2026-03-25T02:59:59.9999999Z
    /// Falls back to treating dates as UTC if timezone is null or invalid.
    /// </summary>
    private static (DateTime UtcStart, DateTime UtcEnd) GetUtcBoundaries(DateTime startDate, DateTime endDate, string? timezone)
    {
        if (!string.IsNullOrEmpty(timezone))
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);

                // Local start-of-day → UTC
                var localStart = new DateTime(startDate.Year, startDate.Month, startDate.Day, 0, 0, 0, DateTimeKind.Unspecified);
                var utcStart = TimeZoneInfo.ConvertTimeToUtc(localStart, tz);

                // Local end-of-day → UTC
                var localEnd = new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 59, 59, DateTimeKind.Unspecified)
                    .AddTicks(9999999); // .9999999 seconds
                var utcEnd = TimeZoneInfo.ConvertTimeToUtc(localEnd, tz);

                return (utcStart, utcEnd);
            }
            catch (TimeZoneNotFoundException)
            {
                // Fall through to default UTC logic
            }
            catch (InvalidTimeZoneException)
            {
                // Fall through to default UTC logic
            }
        }

        // Fallback: treat as UTC
        var start = EnsureUtc(startDate).Date;
        var end = EnsureUtc(endDate).Date.AddDays(1).AddTicks(-1);
        return (start, end);
    }

    /// <summary>
    /// Returns the TimeZoneInfo for the given IANA timezone string, or UTC as fallback.
    /// </summary>
    private static TimeZoneInfo GetTimezone(string? timezone)
    {
        if (!string.IsNullOrEmpty(timezone))
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(timezone); }
            catch { /* fall through */ }
        }
        return TimeZoneInfo.Utc;
    }

    /// <summary>
    /// Extracts the directory component from a file path in a cross-platform way.
    /// Backend runs on Linux in prod, so System.IO.Path may not parse Windows paths correctly.
    /// </summary>
    private static string? GetFolderPathCrossPlatform(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return null;

        var path = filePath.Trim();

        if (path.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(path);
                path = uri.LocalPath;
            }
            catch
            {
                // Ignore and fall back to raw parsing.
            }
        }

        // Trim trailing separators, keeping roots intact.
        while (path.Length > 1 && (path.EndsWith("\\", StringComparison.Ordinal) || path.EndsWith("/", StringComparison.Ordinal)))
        {
            if (path == "/")
                break;
            if (path.Length == 3 && char.IsLetter(path[0]) && path[1] == ':' && (path[2] == '\\' || path[2] == '/'))
                break;
            path = path[..^1];
        }

        var lastSlash = path.LastIndexOf('/');
        var lastBackslash = path.LastIndexOf('\\');
        var lastSep = Math.Max(lastSlash, lastBackslash);

        if (lastSep < 0)
            return path;

        if (lastSep == 2 && path.Length >= 3 && char.IsLetter(path[0]) && path[1] == ':' && (path[2] == '\\' || path[2] == '/'))
            return path[..3].Replace('/', '\\');

        if (lastSep == 0)
            return path[..1];

        return path[..lastSep];
    }

    public async Task<DailyActivityAggregate> GetDailyActivityAggregateAsync(
        Guid userId,
        DateTime date,
        string? timezone = null,
        CancellationToken cancellationToken = default)
    {
        DateTime startOfDay, endOfDay;
        if (!string.IsNullOrEmpty(timezone))
        {
            var (s, e) = GetUtcBoundaries(date, date, timezone);
            startOfDay = s;
            endOfDay = e;
        }
        else
        {
            var utcDate = EnsureUtc(date);
            startOfDay = utcDate.Date;
            endOfDay = startOfDay.AddDays(1).AddTicks(-1);
        }

        // Overlap query: include sessions that OVERLAP with the day, not just ones that start in it.
        // A session starting just before local midnight that ends in the day must be included.
        var dayEndExclusive = endOfDay.AddTicks(1); // exclusive bound for clip arithmetic
        var rawSessions = await _context.ActivitySessions
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.StartedAt <= endOfDay && a.EndedAt > startOfDay)
            .Select(a => new
            {
                a.ProcessName,
                a.StartedAt,
                a.EndedAt,
                a.AppCategory,
                a.AppSubcategory
            })
            .ToListAsync(cancellationToken);

        // Clip each session to the day boundaries so cross-midnight sessions only contribute
        // the portion that falls within this local day.
        var sessions = rawSessions.Select(a => new
        {
            a.ProcessName,
            DurationSeconds = (int)Math.Max(0, (
                (a.EndedAt > dayEndExclusive ? dayEndExclusive : a.EndedAt) -
                (a.StartedAt < startOfDay ? startOfDay : a.StartedAt)
            ).TotalSeconds),
            a.StartedAt,
            a.EndedAt,
            a.AppCategory,
            a.AppSubcategory
        }).ToList();

        _logger?.LogInformation(
            "[DailyAggregate] UserId={UserId} Date={Date} Tz={Tz} Bounds={Start}..{End} RawCount={RawCount} RawSum={RawSum}s",
            userId, date.ToString("yyyy-MM-dd"), timezone,
            startOfDay.ToString("o"), endOfDay.ToString("o"),
            sessions.Count, sessions.Sum(a => a.DurationSeconds));

        // Filter out internal/system apps to match dashboard totals
        sessions = sessions.Where(s => !InternalApps.Contains(s.ProcessName)).ToList();

        _logger?.LogInformation(
            "[DailyAggregate] After internal filter: Count={Count} Sum={Sum}s",
            sessions.Count, sessions.Sum(a => a.DurationSeconds));

        // Load org-level overrides for this user
        var overrides = await GetOverridesForUserAsync(userId, cancellationToken);

        // Agrupar por processo e resolver categoria (with override support)
        var appGroups = sessions
            .GroupBy(a => a.ProcessName)
            .Select(g => new AppAggregate
            {
                ProcessName = g.Key,
                TotalSeconds = g.Sum(a => a.DurationSeconds),
                SessionCount = g.Count(),
                Productivity = ResolveProductivityWithOverrides(g.Key, g.First().AppCategory, overrides),
                Subcategory = ResolveSubcategoryWithOverrides(g.Key, g.First().AppCategory, g.First().AppSubcategory, overrides),
                DisplayName = FormatDisplayName(g.Key)
            })
            .OrderByDescending(a => a.TotalSeconds)
            .ToList();

        var timeBounds = sessions.Count != 0
            ? new {
                // Clip times to day boundaries so cross-midnight sessions don't report
                // a StartedAt from yesterday or an EndedAt from tomorrow.
                FirstActivity = sessions.Min(a => a.StartedAt < startOfDay ? startOfDay : a.StartedAt),
                LastActivity = sessions.Max(a => a.EndedAt > dayEndExclusive ? dayEndExclusive : a.EndedAt)
            }
            : null;

        // Compute total as merged (non-overlapping) intervals so that sessions from
        // multiple devices that overlap in time are not double-counted.
        var clippedIntervals = sessions.Select(s => (
            Start: s.StartedAt < startOfDay ? startOfDay : s.StartedAt,
            End:   s.EndedAt   > dayEndExclusive ? dayEndExclusive : s.EndedAt
        ));
        var mergedTotalSeconds = ComputeMergedSeconds(clippedIntervals);

        return new DailyActivityAggregate
        {
            TotalSeconds = (int)mergedTotalSeconds,
            FirstActivity = timeBounds?.FirstActivity,
            LastActivity = timeBounds?.LastActivity,
            Apps = appGroups
        };
    }

    /// <summary>
    /// Merges overlapping time intervals and returns the total non-overlapping duration in seconds.
    /// Prevents double-counting when the same user has sessions from multiple devices.
    /// </summary>
    private static long ComputeMergedSeconds(IEnumerable<(DateTime Start, DateTime End)> intervals)
    {
        var sorted = intervals
            .Where(i => i.End > i.Start)
            .OrderBy(i => i.Start)
            .ToList();

        if (sorted.Count == 0) return 0;

        var merged = new List<(DateTime Start, DateTime End)> { sorted[0] };
        foreach (var (start, end) in sorted.Skip(1))
        {
            var last = merged[^1];
            if (start <= last.End)
                merged[^1] = (last.Start, end > last.End ? end : last.End);
            else
                merged.Add((start, end));
        }

        return (long)merged.Sum(m => (m.End - m.Start).TotalSeconds);
    }

    public async Task<long> GetDailyIdleSecondsAsync(
        Guid userId,
        DateTime date,
        string? timezone = null,
        CancellationToken cancellationToken = default)
    {
        DateTime startOfDay, endOfDay;
        if (!string.IsNullOrEmpty(timezone))
        {
            var (s, e) = GetUtcBoundaries(date, date, timezone);
            startOfDay = s;
            endOfDay = e;
        }
        else
        {
            var utcDate = EnsureUtc(date);
            startOfDay = utcDate.Date;
            endOfDay = startOfDay.AddDays(1).AddTicks(-1);
        }

        // Overlap query + clip: include idle periods crossing midnight, count only the portion
        // within the local day.
        var dayEndExclusiveIdle = endOfDay.AddTicks(1);
        var idlePeriods = await _context.IdlePeriods
            .AsNoTracking()
            .Where(i => i.UserId == userId && i.StartedAt <= endOfDay && i.EndedAt > startOfDay)
            .Select(i => new { i.StartedAt, i.EndedAt })
            .ToListAsync(cancellationToken);

        return idlePeriods.Sum(i => (long)Math.Max(0, (
            (i.EndedAt > dayEndExclusiveIdle ? dayEndExclusiveIdle : i.EndedAt) -
            (i.StartedAt < startOfDay ? startOfDay : i.StartedAt)
        ).TotalSeconds));
    }

    public async Task<IEnumerable<AppAggregate>> GetTopAppsAsync(
        IReadOnlyList<Guid> userIds,
        DateTime startDate,
        DateTime endDate,
        int limit,
        string? productivityFilter,
        string? timezone = null,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = GetUtcBoundaries(startDate, endDate, timezone);

        // Push GroupBy + Sum to the database — avoids loading every session row into memory.
        // Clipping cross-boundary sessions is done in SQL using GREATEST/LEAST equivalents
        // via EF's Math.Max/Min which translate correctly to PostgreSQL.
        var endExclusive = end.AddTicks(1);

        // Group by ProcessName + category in SQL, then clip and sum duration per group.
        // EF translates this to a single aggregating query rather than fetching all rows.
        var rawGroups = await _context.ActivitySessions
            .AsNoTracking()
            .Where(a => userIds.Contains(a.UserId) && a.StartedAt <= end && a.EndedAt > start)
            .GroupBy(a => new { a.ProcessName, a.AppCategory, a.AppSubcategory })
            .Select(g => new
            {
                g.Key.ProcessName,
                g.Key.AppCategory,
                g.Key.AppSubcategory,
                // Raw sum of durations clipped to the queried window — done in SQL
                TotalSeconds = g.Sum(a =>
                    (int)((a.EndedAt > endExclusive ? endExclusive : a.EndedAt) -
                          (a.StartedAt < start ? start : a.StartedAt)).TotalSeconds),
                SessionCount = g.Count()
            })
            .ToListAsync(cancellationToken);

        // Filter out internal apps (can't push to SQL — HashSet uses OrdinalIgnoreCase)
        var filteredGroups = rawGroups
            .Where(a => !InternalApps.Contains(a.ProcessName))
            .ToList();

        // Load org-level overrides for this user
        var overrides = await GetOverridesForUserAsync(userIds[0], cancellationToken);

        // Merge rows that share the same ProcessName but differ by category (override may change it)
        var appGroups = filteredGroups
            .GroupBy(a => a.ProcessName)
            .Select(g =>
            {
                var totalSecs = g.Sum(a => a.TotalSeconds);
                var dominant = g.OrderByDescending(a => a.TotalSeconds).First();
                var productivity = ResolveProductivityWithOverrides(dominant.ProcessName, dominant.AppCategory, overrides);
                return new AppAggregate
                {
                    ProcessName = dominant.ProcessName,
                    TotalSeconds = totalSecs,
                    SessionCount = g.Sum(a => a.SessionCount),
                    Productivity = productivity,
                    Subcategory = ResolveSubcategoryWithOverrides(dominant.ProcessName, dominant.AppCategory, dominant.AppSubcategory, overrides),
                    DisplayName = FormatDisplayName(dominant.ProcessName)
                };
            })
            .Where(a => productivityFilter == null ||
                        a.Productivity?.Equals(productivityFilter, StringComparison.OrdinalIgnoreCase) == true)
            .OrderByDescending(a => a.TotalSeconds)
            .Take(limit)
            .ToList();

        return appGroups;
    }

    // ========================================================================
    // NOVOS MÉTODOS - CX-155
    // ========================================================================

    // Threshold for long focus block: 25 minutes in seconds
    private const long LongFocusBlockThresholdSeconds = 25 * 60;

    public async Task<IEnumerable<DailySummaryItem>> GetDailySummaryRangeAsync(
        IReadOnlyList<Guid> userIds,
        DateTime startDate,
        DateTime endDate,
        string? timezone = null,
        CancellationToken cancellationToken = default)
    {
        var tz = GetTimezone(timezone);
        var (start, end) = GetUtcBoundaries(startDate, endDate, timezone);

        // Buscar sessões and idle periods — include EndedAt for proper midnight-crossing handling.
        // Exclude internal/system apps (same as agent's local dashboard) to avoid inflating totals
        // with "Tracking Stopped" placeholder sessions and our own UI processes.
        // Use overlap query: include sessions that OVERLAP with the range, not just ones that
        // start within it. A session starting just before local midnight that ends in the range
        // (cross-midnight session) must be included so ClipDuration can split it correctly.
        var rawSessionsForRange = await _context.ActivitySessions
            .AsNoTracking()
            .Where(a => userIds.Contains(a.UserId) && a.StartedAt <= end && a.EndedAt > start)
            .Select(a => new { a.StartedAt, a.EndedAt, a.ProcessName, a.AppCategory, a.AppSubcategory, a.TaskId })
            .ToListAsync(cancellationToken);

        // Compute duration from timestamps to avoid stale DurationSeconds
        var sessions = rawSessionsForRange.Select(a => new {
            a.StartedAt, a.EndedAt,
            DurationSeconds = (int)(a.EndedAt - a.StartedAt).TotalSeconds,
            a.ProcessName, a.AppCategory, a.AppSubcategory, a.TaskId
        }).ToList();

        // Filter out internal/system apps in-memory (EF can't translate HashSet.Contains with OrdinalIgnoreCase)
        sessions = sessions
            .Where(s => !InternalApps.Contains(s.ProcessName))
            .ToList();

        // Load org-level overrides for this user
        var overrides = await GetOverridesForUserAsync(userIds[0], cancellationToken);

        // Overlap query for idle periods: include periods crossing day boundaries.
        // Keep EndedAt so ClipDuration can split cross-midnight idle periods correctly.
        var idlePeriods = (await _context.IdlePeriods
            .AsNoTracking()
            .Where(i => userIds.Contains(i.UserId) && i.StartedAt <= end && i.EndedAt > start)
            .Select(i => new { i.StartedAt, i.EndedAt })
            .ToListAsync(cancellationToken))
            .Select(i => new { i.StartedAt, i.EndedAt, DurationSeconds = (int)(i.EndedAt - i.StartedAt).TotalSeconds })
            .ToList();

        // Agrupar por local date — iterate local days and compute UTC boundaries per day.
        // Sessions are clipped to day boundaries so midnight-crossing sessions are split
        // proportionally: only the portion that overlaps the day counts.
        var result = new List<DailySummaryItem>();
        for (var localDate = startDate.Date; localDate <= endDate.Date; localDate = localDate.AddDays(1))
        {
            var (dayStart, dayEnd) = GetUtcBoundaries(localDate, localDate, timezone);
            var dayEndExclusive = dayEnd.AddTicks(1); // use exclusive end for arithmetic

            // Include sessions that OVERLAP with this day (not just StartedAt within it).
            // A session overlaps if it started before dayEnd AND ended after dayStart.
            var daySessions = sessions
                .Where(s => s.StartedAt <= dayEnd && s.EndedAt > dayStart)
                .ToList();
            // Overlap filter for idle: include any idle period that overlaps with this day
            var dayIdle = idlePeriods
                .Where(i => i.StartedAt < dayEndExclusive && i.EndedAt > dayStart)
                .ToList();

            // Clip each session's duration to the day boundaries.
            // If a session is entirely within the day, use full DurationSeconds.
            // If it crosses midnight, only count the portion that falls in this day.
            long ClipDuration(DateTime sessStart, DateTime sessEnd, int rawDuration)
            {
                var clippedStart = sessStart < dayStart ? dayStart : sessStart;
                var clippedEnd = sessEnd > dayEndExclusive ? dayEndExclusive : sessEnd;
                var clippedSeconds = (long)(clippedEnd - clippedStart).TotalSeconds;
                // Never exceed the raw duration (avoid rounding issues)
                return Math.Clamp(clippedSeconds, 0, rawDuration);
            }

            // Use merged intervals to prevent double-counting overlapping sessions
            // (e.g. from agent restarts that create new sessions for the same time window).
            var clippedActiveIntervals = daySessions.Select(s => (
                Start: s.StartedAt < dayStart ? dayStart : s.StartedAt,
                End:   s.EndedAt > dayEndExclusive ? dayEndExclusive : s.EndedAt
            ));
            var totalActive = ComputeMergedSeconds(clippedActiveIntervals);
            var totalIdle = dayIdle.Sum(i => ClipDuration(i.StartedAt, i.EndedAt, i.DurationSeconds));

            // Calcular produtividade (using merged intervals + overrides).
            // A session that ran while a kanban task was in progress (TaskId set) ALWAYS counts
            // as productive — the user explicitly opted into focused work on a tracked task.
            var clippedProductiveIntervals = daySessions
                .Where(s => s.TaskId != null || ResolveProductivityWithOverrides(s.ProcessName, s.AppCategory, overrides) == "productive")
                .Select(s => (
                    Start: s.StartedAt < dayStart ? dayStart : s.StartedAt,
                    End:   s.EndedAt > dayEndExclusive ? dayEndExclusive : s.EndedAt
                ));
            var productiveSeconds = ComputeMergedSeconds(clippedProductiveIntervals);

            // DEBUG: Log productivity calculation
            var productiveCount = daySessions.Count(s => ResolveProductivityWithOverrides(s.ProcessName, s.AppCategory, overrides) == "productive");
            var neutralCount = daySessions.Count(s => ResolveProductivityWithOverrides(s.ProcessName, s.AppCategory, overrides) == "neutral");
            var distractionCountDebug = daySessions.Count(s => ResolveProductivityWithOverrides(s.ProcessName, s.AppCategory, overrides) == "distraction");
            _logger?.LogInformation(
                "GetDailySummaryRangeAsync Day={Date}: TotalActive={TotalActive}s, Productive={Productive}s ({ProductiveCount} sessions), Neutral={NeutralCount} sessions, Distraction={DistractionCount} sessions",
                localDate, totalActive, productiveSeconds, productiveCount, neutralCount, distractionCountDebug);

            var productivityRatio = totalActive > 0
                ? (double)productiveSeconds / totalActive
                : 0;

            // Calcular Focus Score inline
            short focusScore = 0;
            var distractionCount = 0;
            var longFocusBlockCount = 0;

            if (totalActive > 0)
            {
                // Contar distrações (apps únicos de distração) — task-linked sessions never count.
                distractionCount = daySessions
                    .Where(s => s.TaskId == null && ResolveProductivityWithOverrides(s.ProcessName, s.AppCategory, overrides) == "distraction")
                    .Select(s => s.ProcessName)
                    .Distinct()
                    .Count();

                // Contar blocos de foco longo (>25min consecutivos em apps produtivos OU vinculados a tarefas)
                // Use clipped durations for accurate day-level accounting
                var currentFocusBlockSeconds = 0L;
                foreach (var session in daySessions.OrderBy(s => s.StartedAt))
                {
                    var isProductive = session.TaskId != null
                        || ResolveProductivityWithOverrides(session.ProcessName, session.AppCategory, overrides) == "productive";
                    var durationSeconds = ClipDuration(session.StartedAt, session.EndedAt, session.DurationSeconds);

                    if (isProductive)
                    {
                        currentFocusBlockSeconds += durationSeconds;
                    }
                    else
                    {
                        if (currentFocusBlockSeconds >= LongFocusBlockThresholdSeconds)
                            longFocusBlockCount++;
                        currentFocusBlockSeconds = 0;
                    }
                }
                // Flush final block
                if (currentFocusBlockSeconds >= LongFocusBlockThresholdSeconds)
                    longFocusBlockCount++;

                // Calcular Focus Score (using clipped durations)
                var distractionMs = daySessions
                    .Where(s => s.TaskId == null && ResolveProductivityWithOverrides(s.ProcessName, s.AppCategory, overrides) == "distraction")
                    .Sum(s => ClipDuration(s.StartedAt, s.EndedAt, s.DurationSeconds) * 1000);

                var input = FocusScoreInput.Create(
                    totalTrackedMs: totalActive * 1000,
                    focusTimeMs: productiveSeconds * 1000,
                    distractionMs: distractionMs,
                    distractionCount: distractionCount,
                    pauseCount: 0,
                    idleCount: 0,
                    longFocusBlockCount: longFocusBlockCount);

                focusScore = FocusScoreCalculator.Calculate(input);
            }

            result.Add(new DailySummaryItem
            {
                Date = localDate,
                TotalActiveSeconds = totalActive,
                TotalIdleSeconds = totalIdle,
                ProductivityRatio = productivityRatio,
                FocusScore = focusScore,
                ProductiveSeconds = productiveSeconds,
                DistractionCount = distractionCount,
                LongFocusBlockCount = longFocusBlockCount
            });
        }

        return result;
    }

    public async Task<IEnumerable<ProductivityTrendItem>> GetProductivityTrendAsync(
        IReadOnlyList<Guid> userIds,
        DateTime startDate,
        DateTime endDate,
        string groupBy,
        string? timezone = null,
        CancellationToken cancellationToken = default)
    {
        var tz = GetTimezone(timezone);
        var (start, end) = GetUtcBoundaries(startDate, endDate, timezone);

        var sessions = (await _context.ActivitySessions
            .AsNoTracking()
            .Where(a => userIds.Contains(a.UserId) && a.StartedAt <= end && a.EndedAt > start)
            .Select(a => new { a.StartedAt, a.EndedAt, a.ProcessName, a.AppCategory, a.AppSubcategory })
            .ToListAsync(cancellationToken))
            .Select(a => new {
                a.StartedAt, a.EndedAt,
                DurationSeconds = (int)Math.Max(0, (
                    (a.EndedAt > end.AddTicks(1) ? end.AddTicks(1) : a.EndedAt) -
                    (a.StartedAt < start ? start : a.StartedAt)
                ).TotalSeconds),
                a.ProcessName, a.AppCategory, a.AppSubcategory
            })
            .ToList();

        // Filter out internal/system apps
        sessions = sessions.Where(s => !InternalApps.Contains(s.ProcessName)).ToList();

        // Load org-level overrides for this user
        var overrides = await GetOverridesForUserAsync(userIds[0], cancellationToken);

        // Overlap query for idle periods; keep EndedAt for per-day clipping
        var idlePeriods = (await _context.IdlePeriods
            .AsNoTracking()
            .Where(i => userIds.Contains(i.UserId) && i.StartedAt <= end && i.EndedAt > start)
            .Select(i => new { i.StartedAt, i.EndedAt })
            .ToListAsync(cancellationToken))
            .Select(i => new { i.StartedAt, i.EndedAt, DurationSeconds = (int)(i.EndedAt - i.StartedAt).TotalSeconds })
            .ToList();

        // Convert UTC timestamps to local time for grouping
        DateTime ToLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);

        // For "day" grouping, iterate per day and clip durations (same as DailySummaryRange)
        // to ensure consistency between heatmap and trend chart.
        // For "week"/"month" grouping, use StartedAt-based grouping with full durations.
        if (groupBy.Equals("day", StringComparison.OrdinalIgnoreCase))
        {
            var result = new List<ProductivityTrendItem>();
            for (var localDate = startDate.Date; localDate <= endDate.Date; localDate = localDate.AddDays(1))
            {
                var (dayStart, dayEnd) = GetUtcBoundaries(localDate, localDate, timezone);
                var dayEndExclusive = dayEnd.AddTicks(1);

                // Sessions that OVERLAP this day (same logic as DailySummaryRange)
                var daySessions = sessions.Where(s => s.StartedAt <= dayEnd && s.EndedAt > dayStart).ToList();
                var dayIdle = idlePeriods.Where(i => i.StartedAt < dayEndExclusive && i.EndedAt > dayStart).ToList();

                // Clip durations to day boundaries
                long ClipDuration(DateTime sessStart, DateTime sessEnd, int rawDuration)
                {
                    var clippedStart = sessStart < dayStart ? dayStart : sessStart;
                    var clippedEnd = sessEnd > dayEndExclusive ? dayEndExclusive : sessEnd;
                    return Math.Clamp((long)(clippedEnd - clippedStart).TotalSeconds, 0, rawDuration);
                }

                var productive = daySessions
                    .Where(s => ResolveProductivityWithOverrides(s.ProcessName, s.AppCategory, overrides) == "productive")
                    .Sum(s => ClipDuration(s.StartedAt, s.EndedAt, s.DurationSeconds));
                var distraction = daySessions
                    .Where(s => ResolveProductivityWithOverrides(s.ProcessName, s.AppCategory, overrides) == "distraction")
                    .Sum(s => ClipDuration(s.StartedAt, s.EndedAt, s.DurationSeconds));
                var neutral = daySessions
                    .Where(s => ResolveProductivityWithOverrides(s.ProcessName, s.AppCategory, overrides) == "neutral")
                    .Sum(s => ClipDuration(s.StartedAt, s.EndedAt, s.DurationSeconds));
                var idleSeconds = dayIdle.Sum(i => ClipDuration(i.StartedAt, i.EndedAt, i.DurationSeconds));

                result.Add(new ProductivityTrendItem
                {
                    Period = localDate.ToString("yyyy-MM-dd"),
                    ProductiveSeconds = productive,
                    NeutralSeconds = neutral,
                    DistractionSeconds = distraction,
                    IdleSeconds = idleSeconds
                });
            }
            return result;
        }

        // Week/month grouping: use StartedAt-based grouping with full durations
        var grouped = groupBy.ToLowerInvariant() switch
        {
            "week" => sessions.GroupBy(s => GetWeekKey(ToLocal(s.StartedAt))),
            _ => sessions.GroupBy(s => GetMonthKey(ToLocal(s.StartedAt)))
        };

        var idleGrouped = groupBy.ToLowerInvariant() switch
        {
            "week" => idlePeriods.GroupBy(i => GetWeekKey(ToLocal(i.StartedAt))),
            _ => idlePeriods.GroupBy(i => GetMonthKey(ToLocal(i.StartedAt)))
        };

        var idleDict = idleGrouped.ToDictionary(g => g.Key, g => g.Sum(i => i.DurationSeconds));

        return grouped.Select(g =>
        {
            var items = g.ToList();
            var productive = items
                .Where(s => ResolveProductivityWithOverrides(s.ProcessName, s.AppCategory, overrides) == "productive")
                .Sum(s => (long)s.DurationSeconds);
            var distraction = items
                .Where(s => ResolveProductivityWithOverrides(s.ProcessName, s.AppCategory, overrides) == "distraction")
                .Sum(s => (long)s.DurationSeconds);
            var neutral = items
                .Where(s => ResolveProductivityWithOverrides(s.ProcessName, s.AppCategory, overrides) == "neutral")
                .Sum(s => (long)s.DurationSeconds);

            idleDict.TryGetValue(g.Key, out var idleSeconds);

            return new ProductivityTrendItem
            {
                Period = g.Key,
                ProductiveSeconds = productive,
                NeutralSeconds = neutral,
                DistractionSeconds = distraction,
                IdleSeconds = idleSeconds
            };
        }).OrderBy(t => t.Period).ToList();
    }

    public async Task<IEnumerable<TopPathItem>> GetTopPathsAsync(
        IReadOnlyList<Guid> userIds,
        DateTime startDate,
        DateTime endDate,
        int limit,
        string? timezone = null,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = GetUtcBoundaries(startDate, endDate, timezone);

        var endExclusivePaths = end.AddTicks(1);
        var sessions = (await _context.ActivitySessions
            .AsNoTracking()
            .Where(a => userIds.Contains(a.UserId) && a.StartedAt <= end && a.EndedAt > start)
            .Where(a => a.WindowTitle != null && a.WindowTitle != "")
            .Select(a => new { a.ProcessName, a.WindowTitle, a.FilePath, a.StartedAt, a.EndedAt })
            .ToListAsync(cancellationToken))
            .Select(a => new {
                a.ProcessName, a.WindowTitle, a.FilePath,
                DurationSeconds = (int)Math.Max(0, (
                    (a.EndedAt > endExclusivePaths ? endExclusivePaths : a.EndedAt) -
                    (a.StartedAt < start ? start : a.StartedAt)
                ).TotalSeconds)
            })
            .ToList();

        // Filter out internal/system apps
        sessions = sessions.Where(s => !InternalApps.Contains(s.ProcessName)).ToList();

        // Extrair paths usando FilePath se disponível, senão extrair do WindowTitle
        var paths = sessions
            .Select(s => new
            {
                PathInfo = ExtractPathInfo(s.ProcessName, s.WindowTitle ?? "", s.FilePath),
                s.ProcessName,
                s.DurationSeconds
            })
            .Where(p => !string.IsNullOrEmpty(p.PathInfo.Title) || !string.IsNullOrEmpty(p.PathInfo.FilePath))
            .GroupBy(p => new { p.PathInfo.Title, p.PathInfo.FilePath })
            .Select(g => new TopPathItem
            {
                Title = g.Key.Title,
                FilePath = g.Key.FilePath,
                // Path = combinação de título + filepath para compatibilidade
                Path = !string.IsNullOrEmpty(g.Key.FilePath)
                    ? $"{g.Key.Title} - {g.Key.FilePath}"
                    : g.Key.Title,
                SourceApp = g.First().ProcessName,
                TotalSeconds = g.Sum(p => p.DurationSeconds),
                VisitCount = g.Count()
            })
            .OrderByDescending(p => p.TotalSeconds)
            .Take(limit)
            .ToList();

        return paths;
    }

    public async Task<IEnumerable<TopFolderAggregate>> GetTopFoldersAsync(
        IReadOnlyList<Guid> userIds,
        DateTime startDate,
        DateTime endDate,
        int limit,
        string? timezone = null,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = GetUtcBoundaries(startDate, endDate, timezone);

        var endExclusive = end.AddTicks(1);
        var sessions = (await _context.ActivitySessions
            .AsNoTracking()
            .Where(a => userIds.Contains(a.UserId) && a.StartedAt <= end && a.EndedAt > start)
            .Where(a => a.FilePath != null && a.FilePath != "")
            .Select(a => new { a.ProcessName, a.FilePath, a.StartedAt, a.EndedAt })
            .ToListAsync(cancellationToken))
            .Select(a => new
            {
                a.ProcessName,
                a.FilePath,
                DurationSeconds = (int)Math.Max(0, (
                    (a.EndedAt > endExclusive ? endExclusive : a.EndedAt) -
                    (a.StartedAt < start ? start : a.StartedAt)
                ).TotalSeconds)
            })
            .ToList();

        // Filter out internal/system apps
        sessions = sessions.Where(s => !InternalApps.Contains(s.ProcessName)).ToList();

        var folders = sessions
            .Select(s => new
            {
                FolderPath = GetFolderPathCrossPlatform(s.FilePath!),
                s.DurationSeconds
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.FolderPath))
            .GroupBy(x => x.FolderPath!)
            .Select(g => new TopFolderAggregate
            {
                FolderPath = g.Key,
                TotalSeconds = g.Sum(x => (long)x.DurationSeconds),
                VisitCount = g.Count()
            })
            .OrderByDescending(f => f.TotalSeconds)
            .Take(limit)
            .ToList();

        return folders;
    }

    public async Task<DistractionStats> GetDistractionStatsAsync(
        IReadOnlyList<Guid> userIds,
        DateTime startDate,
        DateTime endDate,
        string? timezone = null,
        CancellationToken cancellationToken = default)
    {
        var tz = GetTimezone(timezone);
        var (start, end) = GetUtcBoundaries(startDate, endDate, timezone);

        var endExclusiveDistr = end.AddTicks(1);
        var sessions = (await _context.ActivitySessions
            .AsNoTracking()
            .Where(a => userIds.Contains(a.UserId) && a.StartedAt <= end && a.EndedAt > start)
            .Select(a => new { a.StartedAt, a.EndedAt, a.ProcessName, a.AppCategory, a.AppSubcategory })
            .ToListAsync(cancellationToken))
            .Select(a => new {
                a.StartedAt, a.ProcessName,
                DurationSeconds = (int)Math.Max(0, (
                    (a.EndedAt > endExclusiveDistr ? endExclusiveDistr : a.EndedAt) -
                    (a.StartedAt < start ? start : a.StartedAt)
                ).TotalSeconds),
                a.AppCategory, a.AppSubcategory
            })
            .ToList();

        // Filter out internal/system apps
        sessions = sessions.Where(s => !InternalApps.Contains(s.ProcessName)).ToList();

        // Load org-level overrides for this user
        var overrides = await GetOverridesForUserAsync(userIds[0], cancellationToken);

        // Filtrar distrações (with override support)
        var distractions = sessions
            .Where(s => ResolveProductivityWithOverrides(s.ProcessName, s.AppCategory, overrides) == "distraction")
            .ToList();

        // Agrupar por dia (local time)
        DateTime ToLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);
        var dailyDistractions = distractions
            .GroupBy(s => ToLocal(s.StartedAt).Date)
            .Select(g => new DailyDistraction
            {
                Date = g.Key,
                DistractionSeconds = g.Sum(s => s.DurationSeconds)
            })
            .OrderBy(d => d.Date)
            .ToList();

        // Top 5 distrações
        var topDistractions = distractions
            .GroupBy(s => s.ProcessName)
            .Select(g => new AppAggregate
            {
                ProcessName = g.Key,
                TotalSeconds = g.Sum(s => s.DurationSeconds),
                SessionCount = g.Count(),
                Productivity = "distraction",
                Subcategory = ResolveSubcategoryWithOverrides(g.Key, g.First().AppCategory, g.First().AppSubcategory, overrides),
                DisplayName = FormatDisplayName(g.Key)
            })
            .OrderByDescending(a => a.TotalSeconds)
            .Take(5)
            .ToList();

        return new DistractionStats
        {
            DailyDistractions = dailyDistractions,
            TopDistractions = topDistractions
        };
    }

    public async Task<IEnumerable<CategoryDistributionItem>> GetCategoryDistributionAsync(
        IReadOnlyList<Guid> userIds,
        DateTime startDate,
        DateTime endDate,
        string? timezone = null,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = GetUtcBoundaries(startDate, endDate, timezone);

        var endExclusiveCat = end.AddTicks(1);
        var sessions = (await _context.ActivitySessions
            .AsNoTracking()
            .Where(a => userIds.Contains(a.UserId) && a.StartedAt <= end && a.EndedAt > start)
            .Select(a => new { a.ProcessName, a.StartedAt, a.EndedAt, a.AppCategory, a.AppSubcategory })
            .ToListAsync(cancellationToken))
            .Select(a => new {
                a.ProcessName,
                DurationSeconds = (int)Math.Max(0, (
                    (a.EndedAt > endExclusiveCat ? endExclusiveCat : a.EndedAt) -
                    (a.StartedAt < start ? start : a.StartedAt)
                ).TotalSeconds),
                a.AppCategory, a.AppSubcategory
            })
            .ToList();

        // Filter out internal/system apps
        sessions = sessions.Where(s => !InternalApps.Contains(s.ProcessName)).ToList();

        // Load org-level overrides for this user
        var overrides = await GetOverridesForUserAsync(userIds[0], cancellationToken);

        var totalSeconds = sessions.Sum(s => s.DurationSeconds);
        if (totalSeconds == 0) return [];

        // Agrupar por categoria principal (with override support)
        var categories = sessions
            .GroupBy(s => ResolveProductivityWithOverrides(s.ProcessName, s.AppCategory, overrides) ?? "neutral")
            .Select(g =>
            {
                var categoryTotal = g.Sum(s => s.DurationSeconds);
                var subcategories = g
                    .GroupBy(s => ResolveSubcategoryWithOverrides(s.ProcessName, s.AppCategory, s.AppSubcategory, overrides) ?? "unknown")
                    .Select(sg => new SubcategoryItem
                    {
                        Name = sg.Key,
                        TotalSeconds = sg.Sum(s => s.DurationSeconds),
                        Percentage = categoryTotal > 0 ? (double)sg.Sum(s => s.DurationSeconds) / categoryTotal * 100 : 0
                    })
                    .OrderByDescending(s => s.TotalSeconds)
                    .ToList();

                return new CategoryDistributionItem
                {
                    Category = g.Key,
                    TotalSeconds = categoryTotal,
                    Percentage = (double)categoryTotal / totalSeconds * 100,
                    Subcategories = subcategories
                };
            })
            .OrderByDescending(c => c.TotalSeconds)
            .ToList();

        return categories;
    }

    // ========================================================================
    // HELPER METHODS
    // ========================================================================

    /// <summary>
    /// Resolve produtividade usando APENAS a categoria salva pelo Agent.
    /// Sem heurísticas - alinhado 100% com o Dashboard.
    /// </summary>
    private string? ResolveProductivity(string? appCategory)
    {
        if (string.IsNullOrEmpty(appCategory))
        {
            _logger?.LogWarning("ResolveProductivity: AppCategory is null or empty, returning 'neutral'");
            return "neutral"; // Default para dados antigos sem categoria
        }

        var cat = appCategory.ToLowerInvariant();
        if (cat.Contains("productive"))
        {
            _logger?.LogDebug("ResolveProductivity: '{AppCategory}' -> 'productive'", appCategory);
            return "productive";
        }
        if (cat.Contains("distraction") || cat.Contains("distração"))
        {
            _logger?.LogDebug("ResolveProductivity: '{AppCategory}' -> 'distraction'", appCategory);
            return "distraction";
        }
        _logger?.LogDebug("ResolveProductivity: '{AppCategory}' -> 'neutral' (no match)", appCategory);
        return "neutral";
    }

    /// <summary>
    /// Resolve subcategoria usando APENAS os dados salvos pelo Agent.
    /// Sem heurísticas - alinhado 100% com o Dashboard.
    /// </summary>
    private static string? ResolveSubcategory(string? appCategory, string? appSubcategory)
    {
        // Prioridade 1: Subcategoria explícita do Agent
        if (!string.IsNullOrEmpty(appSubcategory))
            return appSubcategory.ToLowerInvariant();

        // Prioridade 2: Extrair do appCategory se vier no formato "productive:development"
        if (!string.IsNullOrEmpty(appCategory))
        {
            var parts = appCategory.Split('/', '\\', ':');
            if (parts.Length > 1) return parts[1].ToLowerInvariant();
        }

        // Fallback: unknown para dados antigos
        return "unknown";
    }

    private static string FormatDisplayName(string processName)
    {
        var name = processName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);
        if (name.Length > 0)
        {
            name = char.ToUpperInvariant(name[0]) + name.Substring(1);
        }
        return name.Replace('.', ' ').Replace('-', ' ');
    }

    private static string GetWeekKey(DateTime date)
    {
        // ISO 8601 week
        var dayOfWeek = (int)date.DayOfWeek;
        if (dayOfWeek == 0) dayOfWeek = 7;
        var thursday = date.AddDays(4 - dayOfWeek);
        return $"{thursday.Year}-W{GetIso8601WeekOfYear(thursday):D2}";
    }

    private static int GetIso8601WeekOfYear(DateTime date)
    {
        var day = System.Globalization.CultureInfo.InvariantCulture.Calendar.GetDayOfWeek(date);
        if (day >= System.DayOfWeek.Monday && day <= System.DayOfWeek.Wednesday)
        {
            date = date.AddDays(3);
        }
        return System.Globalization.CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
            date, System.Globalization.CalendarWeekRule.FirstFourDayWeek, System.DayOfWeek.Monday);
    }

    private static string GetMonthKey(DateTime date) => date.ToString("yyyy-MM");

    /// <summary>
    /// Extrai informações estruturadas do windowTitle: Title (projeto/site) e FilePath (arquivo/caminho)
    /// </summary>
    private static (string Title, string? FilePath) ExtractPathInfo(string processName, string windowTitle, string? filePath)
    {
        if (string.IsNullOrEmpty(windowTitle))
            return (string.Empty, filePath);

        var process = processName.ToLowerInvariant();

        // Se já temos FilePath do agent, usar
        if (!string.IsNullOrEmpty(filePath))
        {
            // Extrair título do windowTitle
            var title = ExtractTitleFromWindowTitle(processName, windowTitle);
            return (title, filePath);
        }

        // Browser: extrair site e página
        if (process.Contains("chrome") || process.Contains("firefox") || process.Contains("edge") || process.Contains("brave"))
        {
            return ExtractBrowserPathInfo(windowTitle);
        }

        // VS Code: extrair projeto e arquivo
        if (process.Contains("code"))
        {
            return ExtractVsCodePathInfo(windowTitle);
        }

        // JetBrains IDEs
        if (process.Contains("idea") || process.Contains("webstorm") || process.Contains("rider") || process.Contains("pycharm"))
        {
            return ExtractJetBrainsPathInfo(windowTitle);
        }

        // Explorer: pasta
        if (process.Contains("explorer"))
        {
            return (windowTitle, null);
        }

        // Fallback: usar windowTitle como título
        return (windowTitle, null);
    }

    private static string ExtractTitleFromWindowTitle(string processName, string windowTitle)
    {
        var process = processName.ToLowerInvariant();

        if (process.Contains("chrome") || process.Contains("firefox") || process.Contains("edge") || process.Contains("brave"))
        {
            var dashIndex = windowTitle.LastIndexOf(" - ");
            if (dashIndex > 0)
            {
                var sitePart = windowTitle.Substring(dashIndex + 3).Trim();
                var browserNames = new[] { "Google Chrome", "Mozilla Firefox", "Microsoft Edge", "Brave" };
                foreach (var browser in browserNames)
                {
                    if (sitePart.EndsWith(browser, StringComparison.OrdinalIgnoreCase))
                    {
                        sitePart = sitePart[..^browser.Length].Trim();
                        break;
                    }
                }
                return sitePart;
            }
        }

        if (process.Contains("code"))
        {
            var parts = windowTitle.Split(" - ");
            if (parts.Length >= 2)
            {
                var cleanParts = parts.Where(p =>
                    !p.Contains("Visual Studio Code", StringComparison.OrdinalIgnoreCase) &&
                    !p.Contains("Insiders", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                if (cleanParts.Length >= 2)
                    return cleanParts[^1].Trim();
            }
        }

        return windowTitle;
    }

    private static (string Title, string? FilePath) ExtractBrowserPathInfo(string windowTitle)
    {
        // Padrão: "Título da Página - GitHub - Google Chrome"
        var dashIndex = windowTitle.LastIndexOf(" - ");
        if (dashIndex > 0)
        {
            var sitePart = windowTitle.Substring(dashIndex + 3).Trim();
            var browserNames = new[] { "Google Chrome", "Mozilla Firefox", "Microsoft Edge", "Brave" };
            foreach (var browser in browserNames)
            {
                if (sitePart.EndsWith(browser, StringComparison.OrdinalIgnoreCase))
                {
                    sitePart = sitePart[..^browser.Length].Trim();
                    break;
                }
            }

            var titlePart = windowTitle.Substring(0, dashIndex).Trim();
            if (!string.IsNullOrEmpty(sitePart) && sitePart.Length > 2)
            {
                // Title = site (ex: "GitHub")
                // FilePath = título da página (ex: "Pull Request #123")
                return (sitePart, !string.IsNullOrEmpty(titlePart) ? titlePart : null);
            }
        }

        return (windowTitle, null);
    }

    private static (string Title, string? FilePath) ExtractVsCodePathInfo(string windowTitle)
    {
        // Padrão: "filename.tsx - projectName - Visual Studio Code"
        var parts = windowTitle.Split(" - ");
        if (parts.Length >= 2)
        {
            var cleanParts = parts.Where(p =>
                !p.Contains("Visual Studio Code", StringComparison.OrdinalIgnoreCase) &&
                !p.Contains("Insiders", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (cleanParts.Length >= 2)
            {
                // Title = projectName (ex: "TimeTracking")
                // FilePath = filename (ex: "TopPathsSection.tsx")
                var projectName = cleanParts[^1].Trim();
                var fileName = cleanParts[0].Trim();
                return (projectName, fileName);
            }
            else if (cleanParts.Length == 1)
            {
                return (cleanParts[0].Trim(), null);
            }
        }

        return (windowTitle, null);
    }

    private static (string Title, string? FilePath) ExtractJetBrainsPathInfo(string windowTitle)
    {
        // Padrão: "filename.ts – project-name"
        var dashIndex = windowTitle.IndexOf(" – ");
        if (dashIndex > 0)
        {
            var filePart = windowTitle.Substring(0, dashIndex).Trim();
            var projectPart = windowTitle.Substring(dashIndex + 3).Trim();
            return (projectPart, filePart);
        }

        return (windowTitle, null);
    }
}
