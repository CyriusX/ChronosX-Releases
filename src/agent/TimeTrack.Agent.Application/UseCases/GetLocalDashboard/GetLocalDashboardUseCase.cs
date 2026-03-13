using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Contracts.Services;

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

        // Calcula totais
        var totalWorkTime = TimeSpan.Zero;
        var appUsage = new Dictionary<string, (TimeSpan Time, string Category)>();

        foreach (var session in sessions)
        {
            totalWorkTime += session.Duration;

            var appName = session.App.DisplayName;
            if (appUsage.TryGetValue(appName, out var existing))
            {
                appUsage[appName] = (existing.Time + session.Duration, session.App.Category.Productivity);
            }
            else
            {
                appUsage[appName] = (session.Duration, session.App.Category.Productivity);
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
                ProductivityCategory = x.Value.Category
            })
            .ToList();

        // Última sessão
        var lastSession = sessions.OrderByDescending(s => s.Period.EndUtc).FirstOrDefault();

        _logger.LogDebug(
            "Dashboard generated for {Date}: {SessionCount} sessions, {WorkTime:mm\\:ss} work time",
            targetDate, sessions.Count, totalWorkTime);

        return new LocalDashboardResponse
        {
            Date = targetDate,
            TrackingStatus = state?.Status.ToString() ?? "Unknown",
            TotalWorkTime = totalWorkTime,
            TotalIdleTime = totalIdleTime,
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
}
