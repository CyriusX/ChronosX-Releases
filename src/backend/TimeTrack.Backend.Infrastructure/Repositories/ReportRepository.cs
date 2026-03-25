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

    // Cache em memória das categorias globais (carregado uma vez por instância)
    private Dictionary<string, AppCategoryGlobal>? _categoryCache;
    private readonly object _cacheLock = new();

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

    public async Task<DailyActivityAggregate> GetDailyActivityAggregateAsync(
        Guid userId,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        var utcDate = EnsureUtc(date);
        var startOfDay = utcDate.Date;
        var endOfDay = startOfDay.AddDays(1).AddTicks(-1);

        // Query otimizada com GROUP BY + JOIN para categorias
        var sessions = await _context.ActivitySessions
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.StartedAt >= startOfDay && a.StartedAt <= endOfDay)
            .Select(a => new
            {
                a.ProcessName,
                a.DurationSeconds,
                a.StartedAt,
                a.EndedAt,
                a.AppCategory,
                a.AppSubcategory
            })
            .ToListAsync(cancellationToken);

        // Agrupar por processo e resolver categoria
        var appGroups = sessions
            .GroupBy(a => a.ProcessName)
            .Select(g => new AppAggregate
            {
                ProcessName = g.Key,
                TotalSeconds = g.Sum(a => a.DurationSeconds),
                SessionCount = g.Count(),
                Productivity = ResolveProductivity(g.First().AppCategory),
                Subcategory = ResolveSubcategory(g.First().AppCategory, g.First().AppSubcategory),
                DisplayName = FormatDisplayName(g.Key)
            })
            .OrderByDescending(a => a.TotalSeconds)
            .ToList();

        var timeBounds = sessions.Count != 0
            ? new { FirstActivity = sessions.Min(a => a.StartedAt), LastActivity = sessions.Max(a => a.EndedAt) }
            : null;

        return new DailyActivityAggregate
        {
            TotalSeconds = sessions.Sum(a => a.DurationSeconds),
            FirstActivity = timeBounds?.FirstActivity,
            LastActivity = timeBounds?.LastActivity,
            Apps = appGroups
        };
    }

    public async Task<long> GetDailyIdleSecondsAsync(
        Guid userId,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        var utcDate = EnsureUtc(date);
        var startOfDay = utcDate.Date;
        var endOfDay = startOfDay.AddDays(1).AddTicks(-1);

        return await _context.IdlePeriods
            .AsNoTracking()
            .Where(i => i.UserId == userId && i.StartedAt >= startOfDay && i.StartedAt <= endOfDay)
            .SumAsync(i => (long)i.DurationSeconds, cancellationToken);
    }

    public async Task<IEnumerable<AppAggregate>> GetTopAppsAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        int limit,
        string? productivityFilter,
        string? timezone = null,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = GetUtcBoundaries(startDate, endDate, timezone);

        var sessions = await _context.ActivitySessions
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.StartedAt >= start && a.StartedAt <= end)
            .Select(a => new { a.ProcessName, a.DurationSeconds, a.AppCategory, a.AppSubcategory })
            .ToListAsync(cancellationToken);

        var appGroups = sessions
            .GroupBy(a => a.ProcessName)
            .Select(g =>
            {
                var productivity = ResolveProductivity(g.First().AppCategory);
                return new AppAggregate
                {
                    ProcessName = g.Key,
                    TotalSeconds = g.Sum(a => a.DurationSeconds),
                    SessionCount = g.Count(),
                    Productivity = productivity,
                    Subcategory = ResolveSubcategory(g.First().AppCategory, g.First().AppSubcategory),
                    DisplayName = FormatDisplayName(g.Key)
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
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        string? timezone = null,
        CancellationToken cancellationToken = default)
    {
        var tz = GetTimezone(timezone);
        var (start, end) = GetUtcBoundaries(startDate, endDate, timezone);

        // Buscar sessões e idle periods — include EndedAt for proper midnight-crossing handling
        var sessions = await _context.ActivitySessions
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.StartedAt >= start && a.StartedAt <= end)
            .Select(a => new { a.StartedAt, a.EndedAt, a.DurationSeconds, a.ProcessName, a.AppCategory, a.AppSubcategory })
            .ToListAsync(cancellationToken);

        // DEBUG: Log raw session data
        _logger?.LogInformation(
            "GetDailySummaryRangeAsync: UserId={UserId}, Start={Start}, End={End}, SessionsCount={Count}",
            userId, start, end, sessions.Count);

        // DEBUG: Log unique categories found
        var uniqueCategories = sessions.Select(s => s.AppCategory).Distinct().ToList();
        _logger?.LogInformation(
            "GetDailySummaryRangeAsync: Unique AppCategories: [{Categories}]",
            string.Join(", ", uniqueCategories.Select(c => $"'{c}'")));

        var idlePeriods = await _context.IdlePeriods
            .AsNoTracking()
            .Where(i => i.UserId == userId && i.StartedAt >= start && i.StartedAt <= end)
            .Select(i => new { i.StartedAt, i.DurationSeconds })
            .ToListAsync(cancellationToken);

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
            var dayIdle = idlePeriods
                .Where(i => i.StartedAt <= dayEnd && i.StartedAt >= dayStart)
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

            var totalActive = daySessions.Sum(s => ClipDuration(s.StartedAt, s.EndedAt, s.DurationSeconds));
            var totalIdle = dayIdle.Sum(i => i.DurationSeconds);

            // Calcular produtividade (using clipped durations)
            var productiveSeconds = daySessions
                .Where(s => ResolveProductivity(s.AppCategory) == "productive")
                .Sum(s => ClipDuration(s.StartedAt, s.EndedAt, s.DurationSeconds));

            // DEBUG: Log productivity calculation
            var productiveCount = daySessions.Count(s => ResolveProductivity(s.AppCategory) == "productive");
            var neutralCount = daySessions.Count(s => ResolveProductivity(s.AppCategory) == "neutral");
            var distractionCountDebug = daySessions.Count(s => ResolveProductivity(s.AppCategory) == "distraction");
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
                // Contar distrações (apps únicos de distração)
                distractionCount = daySessions
                    .Where(s => ResolveProductivity(s.AppCategory) == "distraction")
                    .Select(s => s.ProcessName)
                    .Distinct()
                    .Count();

                // Contar blocos de foco longo (>25min consecutivos em apps produtivos)
                // Use clipped durations for accurate day-level accounting
                var currentFocusBlockSeconds = 0L;
                foreach (var session in daySessions.OrderBy(s => s.StartedAt))
                {
                    var category = ResolveProductivity(session.AppCategory);
                    var durationSeconds = ClipDuration(session.StartedAt, session.EndedAt, session.DurationSeconds);

                    if (category == "productive")
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
                    .Where(s => ResolveProductivity(s.AppCategory) == "distraction")
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
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        string groupBy,
        string? timezone = null,
        CancellationToken cancellationToken = default)
    {
        var tz = GetTimezone(timezone);
        var (start, end) = GetUtcBoundaries(startDate, endDate, timezone);

        var sessions = await _context.ActivitySessions
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.StartedAt >= start && a.StartedAt <= end)
            .Select(a => new { a.StartedAt, a.DurationSeconds, a.ProcessName, a.AppCategory, a.AppSubcategory })
            .ToListAsync(cancellationToken);

        var idlePeriods = await _context.IdlePeriods
            .AsNoTracking()
            .Where(i => i.UserId == userId && i.StartedAt >= start && i.StartedAt <= end)
            .Select(i => new { i.StartedAt, i.DurationSeconds })
            .ToListAsync(cancellationToken);

        // Convert UTC timestamps to local time for grouping
        DateTime ToLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);

        // Agrupar por período (using local dates)
        var grouped = groupBy.ToLowerInvariant() switch
        {
            "week" => sessions.GroupBy(s => GetWeekKey(ToLocal(s.StartedAt))),
            "month" => sessions.GroupBy(s => GetMonthKey(ToLocal(s.StartedAt))),
            _ => sessions.GroupBy(s => ToLocal(s.StartedAt).ToString("yyyy-MM-dd"))
        };

        var idleGrouped = groupBy.ToLowerInvariant() switch
        {
            "week" => idlePeriods.GroupBy(i => GetWeekKey(ToLocal(i.StartedAt))),
            "month" => idlePeriods.GroupBy(i => GetMonthKey(ToLocal(i.StartedAt))),
            _ => idlePeriods.GroupBy(i => ToLocal(i.StartedAt).ToString("yyyy-MM-dd"))
        };

        var idleDict = idleGrouped.ToDictionary(g => g.Key, g => g.Sum(i => i.DurationSeconds));

        return grouped.Select(g =>
        {
            var items = g.ToList();
            var productive = items
                .Where(s => ResolveProductivity(s.AppCategory) == "productive")
                .Sum(s => s.DurationSeconds);
            var distraction = items
                .Where(s => ResolveProductivity(s.AppCategory) == "distraction")
                .Sum(s => s.DurationSeconds);
            var neutral = items
                .Where(s => ResolveProductivity(s.AppCategory) == "neutral")
                .Sum(s => s.DurationSeconds);

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
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        int limit,
        string? timezone = null,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = GetUtcBoundaries(startDate, endDate, timezone);

        var sessions = await _context.ActivitySessions
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.StartedAt >= start && a.StartedAt <= end)
            .Where(a => a.WindowTitle != null && a.WindowTitle != "")
            .Select(a => new { a.ProcessName, a.WindowTitle, a.FilePath, a.DurationSeconds })
            .ToListAsync(cancellationToken);

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

    public async Task<DistractionStats> GetDistractionStatsAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        string? timezone = null,
        CancellationToken cancellationToken = default)
    {
        var tz = GetTimezone(timezone);
        var (start, end) = GetUtcBoundaries(startDate, endDate, timezone);

        var sessions = await _context.ActivitySessions
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.StartedAt >= start && a.StartedAt <= end)
            .Select(a => new { a.StartedAt, a.ProcessName, a.DurationSeconds, a.AppCategory, a.AppSubcategory })
            .ToListAsync(cancellationToken);

        // Filtrar distrações
        var distractions = sessions
            .Where(s => ResolveProductivity(s.AppCategory) == "distraction")
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
                Subcategory = ResolveSubcategory(g.First().AppCategory, g.First().AppSubcategory),
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
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        string? timezone = null,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = GetUtcBoundaries(startDate, endDate, timezone);

        var sessions = await _context.ActivitySessions
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.StartedAt >= start && a.StartedAt <= end)
            .Select(a => new { a.ProcessName, a.DurationSeconds, a.AppCategory, a.AppSubcategory })
            .ToListAsync(cancellationToken);

        var totalSeconds = sessions.Sum(s => s.DurationSeconds);
        if (totalSeconds == 0) return [];

        // Agrupar por categoria principal
        var categories = sessions
            .GroupBy(s => ResolveProductivity(s.AppCategory) ?? "neutral")
            .Select(g =>
            {
                var categoryTotal = g.Sum(s => s.DurationSeconds);
                var subcategories = g
                    .GroupBy(s => ResolveSubcategory(s.AppCategory, s.AppSubcategory) ?? "unknown")
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
