using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Application.Services;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Domain.Entities;

namespace TimeTrack.Agent.Application.UseCases.GetLocalDashboard;

/// <summary>
/// Use Case para obter dados do dashboard local do Agent
/// </summary>
public sealed class GetLocalDashboardUseCase
{
    private readonly ITrackingStateRepository _stateRepository;
    private readonly IActivitySessionRepository _sessionRepository;
    private readonly IIdlePeriodRepository _idleRepository;
    private readonly ICurrentUserContext _userContext;
    private readonly ILogger<GetLocalDashboardUseCase> _logger;

    // Threshold for long focus block: 25 minutes in milliseconds
    private const long LongFocusBlockThresholdMs = 25 * 60 * 1000;

    public GetLocalDashboardUseCase(
        ITrackingStateRepository stateRepository,
        IActivitySessionRepository sessionRepository,
        IIdlePeriodRepository idleRepository,
        ICurrentUserContext userContext,
        ILogger<GetLocalDashboardUseCase> logger)
    {
        _stateRepository = stateRepository ?? throw new ArgumentNullException(nameof(stateRepository));
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _idleRepository = idleRepository ?? throw new ArgumentNullException(nameof(idleRepository));
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
                Date = date?.Date ?? DateTime.UtcNow.Date,
                TrackingStatus = "NotAuthenticated",
                TotalWorkTime = TimeSpan.Zero,
                TotalIdleTime = TimeSpan.Zero,
                SessionCount = 0,
                TopApplications = new List<AppUsageSummary>(),
                LastSession = null
            };
        }

        var targetDate = date?.Date ?? DateTime.UtcNow.Date;

        var userIdValue = userId.Value;

        // Busca dados em paralelo (filtrados por usuário)
        var stateTask = _stateRepository.GetAsync(userIdValue, cancellationToken);
        var sessionsTask = _sessionRepository.GetByDateAsync(userIdValue, targetDate, cancellationToken);
        var idlePeriodsTask = _idleRepository.GetByDateAsync(userIdValue, targetDate, cancellationToken);

        await Task.WhenAll(stateTask, sessionsTask, idlePeriodsTask);

        var state = await stateTask;
        var sessions = await sessionsTask;
        var idlePeriods = await idlePeriodsTask;

        // Internal app names to exclude from dashboard (our own UI processes)
        var internalApps = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "TimeTrack.DesktopHost",
            "Microsoft Edge WebView2",
            "Microsoft® Windows® Operating System"
        };

        // Calcula totais (excluding our own app processes)
        var totalWorkTime = TimeSpan.Zero;
        var appUsage = new Dictionary<string, (TimeSpan Time, string Category, string Subcategory)>();

        foreach (var session in sessions)
        {
            var appName = session.App.DisplayName;
            if (internalApps.Contains(appName))
                continue;

            totalWorkTime += session.Duration;

            if (appUsage.TryGetValue(appName, out var existing))
            {
                appUsage[appName] = (existing.Time + session.Duration, session.App.Category.Productivity, session.App.Category.Subcategory);
            }
            else
            {
                appUsage[appName] = (session.Duration, session.App.Category.Productivity, session.App.Category.Subcategory);
            }
        }

        var totalIdleTime = idlePeriods.Aggregate(
            TimeSpan.Zero,
            (acc, p) => acc + p.Duration);

        // Monta top aplicações
        var topApps = appUsage
            .OrderByDescending(x => x.Value.Time)
            .Take(5)
            .Select(x => new AppUsageSummary
            {
                DisplayName = x.Key,
                TotalTime = x.Value.Time,
                Percentage = totalWorkTime.TotalSeconds > 0
                    ? (x.Value.Time.TotalSeconds / totalWorkTime.TotalSeconds) * 100
                    : 0,
                ProductivityCategory = x.Value.Category,
                Subcategory = x.Value.Subcategory
            })
            .ToList();

        // Última sessão (excluding internal apps)
        var lastSession = sessions
            .Where(s => !internalApps.Contains(s.App.DisplayName))
            .OrderByDescending(s => s.Period.EndUtc)
            .FirstOrDefault();

        // Calcular métricas de foco
        var focusMetrics = CalculateFocusMetrics(sessions, idlePeriods, appUsage, totalWorkTime);
        var focusScore = FocusScoreCalculator.Calculate(focusMetrics);

        _logger.LogDebug(
            "Dashboard generated for {Date}: {SessionCount} sessions, {WorkTime:mm\\:ss} work time, FocusScore: {FocusScore}",
            targetDate, sessions.Count, totalWorkTime, focusScore);

        return new LocalDashboardResponse
        {
            Date = targetDate,
            TrackingStatus = state?.Status.ToString() ?? "Unknown",
            TotalWorkTime = totalWorkTime,
            TotalIdleTime = totalIdleTime,
            FocusTimeMs = focusMetrics.FocusTimeMs,
            FocusScore = focusScore,
            SessionCount = sessions.Count,
            TopApplications = topApps,
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
        TimeSpan totalWorkTime)
    {
        long focusTimeMs = 0;
        long distractionMs = 0;
        int distractionCount = 0;

        // Aggregate focus/distraction totals from per-app data (order-independent)
        foreach (var kvp in appUsage)
        {
            var timeMs = (long)kvp.Value.Time.TotalMilliseconds;
            var category = kvp.Value.Category;

            if (category == "Productive" || category == "Focus")
                focusTimeMs += timeMs;
            else if (category == "Distraction")
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
            var category = session.App.Category.Productivity;
            var sessionMs = (long)session.Duration.TotalMilliseconds;

            if (category == "Productive" || category == "Focus")
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
}
