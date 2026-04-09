using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Application.Services;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Domain.Entities;
using TimeTrack.Agent.Domain.ValueObjects;

namespace TimeTrack.Agent.Application.UseCases.GetLocalDashboard;

/// <summary>
/// Use Case para obter dados do dashboard local do Agent
/// </summary>
public sealed class GetLocalDashboardUseCase
{
    private readonly ITrackingStateRepository _stateRepository;
    private readonly IActivitySessionRepository _sessionRepository;
    private readonly IIdlePeriodRepository _idleRepository;
    private readonly IAppCategoryCacheRepository _categoryCacheRepository;
    private readonly ICurrentUserContext _userContext;
    private readonly ILogger<GetLocalDashboardUseCase> _logger;

    // Threshold for long focus block: 25 minutes in milliseconds
    private const long LongFocusBlockThresholdMs = 25 * 60 * 1000;

    public GetLocalDashboardUseCase(
        ITrackingStateRepository stateRepository,
        IActivitySessionRepository sessionRepository,
        IIdlePeriodRepository idleRepository,
        IAppCategoryCacheRepository categoryCacheRepository,
        ICurrentUserContext userContext,
        ILogger<GetLocalDashboardUseCase> logger)
    {
        _stateRepository = stateRepository ?? throw new ArgumentNullException(nameof(stateRepository));
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _idleRepository = idleRepository ?? throw new ArgumentNullException(nameof(idleRepository));
        _categoryCacheRepository = categoryCacheRepository ?? throw new ArgumentNullException(nameof(categoryCacheRepository));
        _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executa a obtenção do dashboard para uma data específica
    /// </summary>
    public async Task<LocalDashboardResponse> ExecuteAsync(
        DateTime? date = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("GetLocalDashboardUseCase: Starting ExecuteAsync");

        var userId = _userContext.UserId;

        _logger.LogInformation("GetLocalDashboardUseCase: UserId = {UserId}", userId);

        if (!userId.HasValue)
        {
            _logger.LogWarning("GetLocalDashboardUseCase: User not authenticated, returning empty dashboard");
            // Return empty dashboard instead of throwing
            return new LocalDashboardResponse
            {
                Date = date?.Date ?? DateTime.Today,
                TrackingStatus = "NotAuthenticated",
                TotalWorkTime = TimeSpan.Zero,
                TotalIdleTime = TimeSpan.Zero,
                SessionCount = 0,
                TopApplications = new List<AppUsageSummary>(),
                LastSession = null
            };
        }

        var targetDate = date?.Date ?? DateTime.Today;

        var userIdValue = userId.Value;

        // Busca dados em paralelo (filtrados por usuário)
        var stateTask = _stateRepository.GetAsync(userIdValue, cancellationToken);
        var sessionsTask = _sessionRepository.GetByDateAsync(userIdValue, targetDate, cancellationToken);
        var idlePeriodsTask = _idleRepository.GetByDateAsync(userIdValue, targetDate, cancellationToken);

        await Task.WhenAll(stateTask, sessionsTask, idlePeriodsTask);

        var state = await stateTask;
        var sessions = await sessionsTask;
        var idlePeriods = await idlePeriodsTask;

        // Build override lookup from local category cache (includes org overrides synced from backend)
        // Never let cache failure break the dashboard
        CategoryLookup categoryLookup;
        try
        {
            var cacheEntries = await _categoryCacheRepository.GetAllAsync();
            categoryLookup = BuildCategoryLookup(cacheEntries);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load category cache, using baked-in categories");
            categoryLookup = new CategoryLookup();
        }

        // Internal app names to exclude from dashboard (our own UI processes)
        var internalApps = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "TimeTrack.DesktopHost",
            "Microsoft Edge WebView2",
            "Microsoft® Windows® Operating System",
            "Sistema operacional Microsoft® Windows®",
            "Tracking Stopped",
        };

        // Calcula totais (excluding our own app processes)
        var totalWorkTime = TimeSpan.Zero;
        // Per-app aggregation: tracks total time AND time per category to pick the dominant one
        var appUsage = new Dictionary<string, (TimeSpan Time, Dictionary<string, (TimeSpan Time, string Subcategory)> CategoryBreakdown)>();
        // Aggregate by executable (ExePathHash) — groups browser tabs into their parent app
        var exeUsage = new Dictionary<string, (TimeSpan Time, string Name, Dictionary<string, (TimeSpan Time, string Subcategory)> CategoryBreakdown)>();

        foreach (var session in sessions)
        {
            var appName = session.App.DisplayName;
            if (internalApps.Contains(appName))
                continue;

            // Also skip if the original exe name (from window title) is internal
            var exeName = ExtractExeDisplayName(session.WindowTitle, appName);
            if (internalApps.Contains(exeName))
                continue;

            totalWorkTime += session.Duration;
            // Resolve category from cache (includes org overrides), fall back to baked-in
            var (cat, sub) = ResolveCategory(session, categoryLookup);

            // Per-app aggregation with category breakdown
            if (!appUsage.TryGetValue(appName, out var existing))
            {
                existing = (TimeSpan.Zero, new Dictionary<string, (TimeSpan, string)>());
                appUsage[appName] = existing;
            }
            existing.Time += session.Duration;
            if (existing.CategoryBreakdown.TryGetValue(cat, out var catEntry))
                existing.CategoryBreakdown[cat] = (catEntry.Time + session.Duration, sub);
            else
                existing.CategoryBreakdown[cat] = (session.Duration, sub);
            appUsage[appName] = existing;

            // Per-executable aggregation with category breakdown
            var exeHash = session.App.ExePathHash;
            if (!exeUsage.TryGetValue(exeHash, out var exeExisting))
            {
                exeExisting = (TimeSpan.Zero, exeName, new Dictionary<string, (TimeSpan, string)>());
                exeUsage[exeHash] = exeExisting;
            }
            exeExisting.Time += session.Duration;
            if (exeExisting.CategoryBreakdown.TryGetValue(cat, out var exeCatEntry))
                exeExisting.CategoryBreakdown[cat] = (exeCatEntry.Time + session.Duration, sub);
            else
                exeExisting.CategoryBreakdown[cat] = (session.Duration, sub);
            exeUsage[exeHash] = exeExisting;
        }

        var totalIdleTime = idlePeriods.Aggregate(
            TimeSpan.Zero,
            (acc, p) => acc + p.Duration);

        // Monta top aplicações — picks the dominant category (most time spent) per app
        var topApps = appUsage
            .OrderByDescending(x => x.Value.Time)
            .Take(5)
            .Select(x =>
            {
                var dominant = x.Value.CategoryBreakdown
                    .OrderByDescending(c => c.Value.Time)
                    .First();
                return new AppUsageSummary
                {
                    DisplayName = x.Key,
                    TotalTime = x.Value.Time,
                    Percentage = totalWorkTime.TotalSeconds > 0
                        ? (x.Value.Time.TotalSeconds / totalWorkTime.TotalSeconds) * 100
                        : 0,
                    ProductivityCategory = dominant.Key,
                    Subcategory = dominant.Value.Subcategory
                };
            })
            .ToList();

        // Última sessão (excluding internal apps)
        var lastSession = sessions
            .Where(s => !internalApps.Contains(s.App.DisplayName))
            .OrderByDescending(s => s.Period.EndUtc)
            .FirstOrDefault();

        // Top apps grouped by executable (browser tabs merged into parent app)
        var topAppsByExe = exeUsage
            .OrderByDescending(x => x.Value.Time)
            .Take(5)
            .Select(x =>
            {
                var dominant = x.Value.CategoryBreakdown
                    .OrderByDescending(c => c.Value.Time)
                    .First();
                return new AppUsageSummary
                {
                    DisplayName = x.Value.Name,
                    TotalTime = x.Value.Time,
                    Percentage = totalWorkTime.TotalSeconds > 0
                        ? (x.Value.Time.TotalSeconds / totalWorkTime.TotalSeconds) * 100
                        : 0,
                    ProductivityCategory = dominant.Key,
                    Subcategory = dominant.Value.Subcategory
                };
            })
            .ToList();

        // Calcular métricas de foco — build flat category map from the breakdown
        var flatAppUsage = new Dictionary<string, (TimeSpan Time, string Category, string Subcategory)>();
        foreach (var kvp in appUsage)
        {
            var dominant = kvp.Value.CategoryBreakdown
                .OrderByDescending(c => c.Value.Time)
                .First();
            flatAppUsage[kvp.Key] = (kvp.Value.Time, dominant.Key, dominant.Value.Subcategory);
        }
        var focusMetrics = CalculateFocusMetrics(sessions, idlePeriods, flatAppUsage, totalWorkTime, categoryLookup);
        var focusScore = FocusScoreCalculator.Calculate(focusMetrics);

        _logger.LogDebug(
            "Dashboard generated for {Date}: {SessionCount} sessions, {WorkTime:mm\\:ss} work time, FocusScore: {FocusScore}",
            targetDate, sessions.Count, totalWorkTime, focusScore);

        return new LocalDashboardResponse
        {
            Date = targetDate,
            TrackingStatus = state?.Status.ToString() ?? "Active",
            TotalWorkTime = totalWorkTime,
            TotalIdleTime = totalIdleTime,
            FocusTimeMs = focusMetrics.FocusTimeMs,
            FocusScore = focusScore,
            SessionCount = sessions.Count,
            TopApplications = topApps,
            TopAppsByExe = topAppsByExe,
            LastSession = lastSession != null
                ? new ActivitySessionSummary
                {
                    AppName = lastSession.App.DisplayName,
                    WindowTitle = lastSession.WindowTitle,
                    StartUtc = lastSession.Period.StartUtc,
                    EndUtc = lastSession.Period.EndUtc,
                    Duration = lastSession.Duration
                }
                : null
        };
    }

    /// <summary>
    /// Calcula as métricas de foco para o cálculo do Focus Score
    /// SRP: Apenas calcula métricas de foco
    /// </summary>
    private FocusScoreMetrics CalculateFocusMetrics(
        IEnumerable<ActivitySession> sessions,
        IEnumerable<IdlePeriod> idlePeriods,
        Dictionary<string, (TimeSpan Time, string Category, string Subcategory)> appUsage,
        TimeSpan totalWorkTime,
        CategoryLookup categoryLookup)
    {
        long focusTimeMs = 0;
        long distractionMs = 0;
        int distractionCount = 0;

        // Aggregate focus/distraction totals from per-app data (order-independent)
        foreach (var kvp in appUsage)
        {
            var timeMs = (long)kvp.Value.Time.TotalMilliseconds;
            var category = kvp.Value.Category;

            if (string.Equals(category, "productive", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(category, "focus", StringComparison.OrdinalIgnoreCase))
                focusTimeMs += timeMs;
            else if (string.Equals(category, "distraction", StringComparison.OrdinalIgnoreCase))
            {
                distractionMs += timeMs;
                distractionCount++;
            }
        }

        // Count long focus blocks from chronological session order.
        // A "block" is a consecutive run of productive sessions >= 25 minutes;
        // it resets whenever a distraction or neutral session interrupts it.
        int longFocusBlockCount = 0;
        long currentFocusBlockMs = 0;

        foreach (var session in sessions.OrderBy(s => s.Period.StartUtc))
        {
            var (cat, _) = ResolveCategory(session, categoryLookup);
            var sessionMs = (long)session.Duration.TotalMilliseconds;

            if (string.Equals(cat, "productive", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(cat, "focus", StringComparison.OrdinalIgnoreCase))
            {
                currentFocusBlockMs += sessionMs;
            }
            else
            {
                if (currentFocusBlockMs >= LongFocusBlockThresholdMs)
                    longFocusBlockCount++;
                currentFocusBlockMs = 0;
            }
        }

        // Flush final block
        if (currentFocusBlockMs >= LongFocusBlockThresholdMs)
            longFocusBlockCount++;

        var pauseCount = 0; // TODO: Implement pause tracking
        var idleCount = idlePeriods.Count();

        return FocusScoreMetrics.Create(
            (long)totalWorkTime.TotalMilliseconds,
            focusTimeMs,
            distractionMs,
            distractionCount,
            pauseCount,
            idleCount,
            longFocusBlockCount);
    }

    /// <summary>
    /// Extracts the real application name from a window title.
    /// For browsers: "YouTube - Google Chrome" → "Google Chrome"
    /// For regular apps: returns the displayName as-is.
    /// </summary>
    private static string ExtractExeDisplayName(string? windowTitle, string displayName)
    {
        if (string.IsNullOrWhiteSpace(windowTitle))
            return displayName;

        // Check if the window title has a " - AppName" suffix (typical browser pattern)
        var separators = new[] { " - ", " — ", " – " };
        foreach (var sep in separators)
        {
            var lastIdx = windowTitle.LastIndexOf(sep, StringComparison.Ordinal);
            if (lastIdx > 0)
            {
                var suffix = windowTitle[(lastIdx + sep.Length)..].Trim();
                // If the suffix looks like a real app name (not too short, not the same as display)
                if (suffix.Length > 2 && suffix != displayName)
                    return suffix;
            }
        }

        return displayName;
    }

    // ============================================================================
    // CATEGORY OVERRIDE RESOLUTION
    // ============================================================================

    /// <summary>
    /// Lookup structure for resolving categories from the synced cache (includes org overrides).
    /// </summary>
    private sealed class CategoryLookup
    {
        /// <summary>DisplayName (lowercase) → (productivity, subcategory)</summary>
        public Dictionary<string, (string Productivity, string Subcategory)> ByDisplayName { get; init; } = new();
        /// <summary>ProcessName/Identifier (lowercase) → (productivity, subcategory)</summary>
        public Dictionary<string, (string Productivity, string Subcategory)> ByProcessName { get; init; } = new();
        /// <summary>Domain identifier (lowercase) → (productivity, subcategory)</summary>
        public Dictionary<string, (string Productivity, string Subcategory)> ByDomain { get; init; } = new();

        public bool IsEmpty => ByDisplayName.Count == 0 && ByProcessName.Count == 0 && ByDomain.Count == 0;
    }

    /// <summary>
    /// Builds a triple lookup (by display name + process name + domain) from the local category cache.
    /// The cache is synced from the backend and is the single source of truth for categories.
    /// </summary>
    private static CategoryLookup BuildCategoryLookup(IReadOnlyList<AppCategoryCache> cacheEntries)
    {
        var byDisplayName = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);
        var byProcessName = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);
        var byDomain = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in cacheEntries)
        {
            var productivity = entry.Productivity switch
            {
                AppProductivityCategory.Productive => "productive",
                AppProductivityCategory.Distraction => "distraction",
                _ => "neutral"
            };
            var subcategory = entry.Subcategory ?? "unknown";

            // Index by display name (for exe apps)
            if (!string.IsNullOrEmpty(entry.DisplayName))
                byDisplayName[entry.DisplayName] = (productivity, subcategory);

            // Index by identifier/process name (matches backend's resolution logic)
            if (!string.IsNullOrEmpty(entry.Identifier) && entry.IdentifierType != AppIdentifierType.Domain)
                byProcessName[entry.Identifier] = (productivity, subcategory);

            // Index by identifier for domain-type entries (for browser overrides)
            if (entry.IdentifierType == AppIdentifierType.Domain && !string.IsNullOrEmpty(entry.Identifier))
                byDomain[entry.Identifier] = (productivity, subcategory);
        }

        return new CategoryLookup { ByDisplayName = byDisplayName, ByProcessName = byProcessName, ByDomain = byDomain };
    }

    /// <summary>
    /// Resolves category for a session using the cloud-synced cache as single source of truth.
    /// Priority: domain override > process name > display name > baked-in session category.
    /// </summary>
    private static (string Productivity, string Subcategory) ResolveCategory(
        ActivitySession session, CategoryLookup lookup)
    {
        if (!lookup.IsEmpty)
        {
            // 1. Try domain match (for browser tabs with domain overrides)
            if (!string.IsNullOrEmpty(session.Domain) &&
                lookup.ByDomain.TryGetValue(session.Domain, out var domainMatch))
                return domainMatch;

            // 2. Try process name/identifier match (same key as backend uses)
            var identifier = session.App.ExePathHash;
            if (!string.IsNullOrEmpty(identifier))
            {
                if (lookup.ByProcessName.TryGetValue(identifier, out var procMatch))
                    return procMatch;
                if (identifier.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                    lookup.ByProcessName.TryGetValue(identifier[..^4], out procMatch))
                    return procMatch;
            }

            // 3. Try display name match
            if (lookup.ByDisplayName.TryGetValue(session.App.DisplayName, out var nameMatch))
                return nameMatch;
        }

        // 4. Fall back to baked-in category (only for apps not yet in cloud DB)
        return (session.App.Category.Productivity, session.App.Category.Subcategory);
    }
}
