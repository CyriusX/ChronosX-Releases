using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.AI.Interfaces;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Jobs;

/// <summary>
/// Coleta dados para o relatorio semanal por e-mail.
/// SRP: apenas coleta e estrutura dados para alimentar o AI prompt.
/// </summary>
public sealed class WeeklyReportDataCollector
{
    private readonly TimeTrackDbContext _context;

    public WeeklyReportDataCollector(TimeTrackDbContext context)
    {
        _context = context;
    }

    public async Task<WeeklyEmailReportContext> CollectAsync(
        Guid userId,
        Guid orgId,
        DateOnly weekStart,
        DateOnly weekEnd,
        ReportPreferences preferences,
        CancellationToken ct = default)
    {
        var weekScores = await _context.DailyFocusScores
            .IgnoreQueryFilters()
            .Where(d => d.UserId == userId && d.Date >= weekStart && d.Date <= weekEnd)
            .ToListAsync(ct);

        var prevStart = weekStart.AddDays(-7);
        var prevScores = await _context.DailyFocusScores
            .IgnoreQueryFilters()
            .Where(d => d.UserId == userId && d.Date >= prevStart && d.Date < weekStart)
            .ToListAsync(ct);

        var user = await _context.Users
            .IgnoreQueryFilters()
            .Where(u => u.Id == userId)
            .Select(u => new { u.DisplayName })
            .FirstOrDefaultAsync(ct);

        var currentWeek = BuildMetrics(weekScores);
        var previousWeek = prevScores.Count >= 2 ? BuildMetrics(prevScores) : null;

        List<TeamMemberRanking> rankings = [];
        if (preferences.IncludeTeamComparison)
            rankings = await GetTeamRankingsAsync(orgId, userId, weekStart, weekEnd, ct);

        List<DifficultyItem> difficulties = [];
        if (preferences.IncludeDifficultyAnalysis)
            difficulties = await GetDifficultiesAsync(userId, weekStart, weekEnd, ct);

        List<UnproductiveDay> unproductiveDays = [];
        if (preferences.IncludeUnproductiveDays)
            unproductiveDays = GetUnproductiveDays(weekScores);

        var topApps = await GetTopAppsAsync(userId, weekStart, weekEnd, ct);

        return new WeeklyEmailReportContext
        {
            UserName = user?.DisplayName ?? "Usuario",
            Period = $"{weekStart:dd/MM/yyyy} a {weekEnd:dd/MM/yyyy}",
            CurrentWeek = currentWeek,
            PreviousWeek = previousWeek,
            TeamRankings = rankings,
            TopDifficulties = difficulties,
            UnproductiveDays = unproductiveDays,
            TopApps = topApps,
            Preferences = preferences
        };
    }

    private static UserWeekMetrics BuildMetrics(List<DailyFocusScore> scores)
    {
        if (scores.Count == 0)
            return new UserWeekMetrics();

        var totalProductive = scores.Sum(s => s.ProductiveSeconds);
        var totalDistraction = scores.Sum(s => s.DistractionSeconds);
        var totalNeutral = scores.Sum(s => s.NeutralSeconds);
        var totalSeconds = totalProductive + totalDistraction + totalNeutral;

        return new UserWeekMetrics
        {
            TotalActiveHours = Math.Round(totalSeconds / 3600.0, 1),
            AvgFocusScore = Math.Round(scores.Average(s => (double)s.FocusScore), 1),
            ProductiveRatio = totalSeconds > 0 ? Math.Round((double)totalProductive / totalSeconds, 3) : 0,
            ContextSwitches = scores.Sum(s => s.ContextSwitchesCount),
            InterruptionCount = scores.Sum(s => s.InterruptionCount)
        };
    }

    private async Task<List<TeamMemberRanking>> GetTeamRankingsAsync(
        Guid orgId, Guid userId, DateOnly weekStart, DateOnly weekEnd, CancellationToken ct)
    {
        var orgScores = await _context.DailyFocusScores
            .IgnoreQueryFilters()
            .Where(d => d.OrgId == orgId && d.Date >= weekStart && d.Date <= weekEnd)
            .GroupBy(d => d.UserId)
            .Select(g => new { UserId = g.Key, AvgFocus = g.Average(s => (double)s.FocusScore) })
            .OrderByDescending(x => x.AvgFocus)
            .ToListAsync(ct);

        var userIds = orgScores.Select(s => s.UserId).ToHashSet();
        var users = await _context.Users
            .IgnoreQueryFilters()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName })
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        return orgScores.Select((s, i) => new TeamMemberRanking
        {
            Name = users.GetValueOrDefault(s.UserId, "Desconhecido"),
            FocusScore = Math.Round(s.AvgFocus, 1),
            ProductiveRatio = 0,
            Position = i + 1
        }).ToList();
    }

    private async Task<List<DifficultyItem>> GetDifficultiesAsync(
        Guid userId, DateOnly weekStart, DateOnly weekEnd, CancellationToken ct)
    {
        var weekStartUtc = DateTime.SpecifyKind(weekStart.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var weekEndUtc = DateTime.SpecifyKind(weekEnd.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

        var distractionApps = await _context.ActivitySessions
            .IgnoreQueryFilters()
            .Where(a => a.UserId == userId
                && a.StartedAt >= weekStartUtc
                && a.StartedAt <= weekEndUtc
                && a.AppCategory == "distracting")
            .GroupBy(a => new { a.ProcessName, a.AppCategory })
            .Select(g => new
            {
                AppName = g.Key.ProcessName,
                Category = g.Key.AppCategory ?? "uncategorized",
                TotalSeconds = g.Sum(s => s.DurationSeconds)
            })
            .OrderByDescending(x => x.TotalSeconds)
            .Take(5)
            .ToListAsync(ct);

        return distractionApps.Select(d => new DifficultyItem
        {
            AppName = d.AppName,
            Hours = Math.Round(d.TotalSeconds / 3600.0, 1),
            Category = d.Category
        }).ToList();
    }

    private static List<UnproductiveDay> GetUnproductiveDays(List<DailyFocusScore> scores)
    {
        var dayNames = new Dictionary<DayOfWeek, string>
        {
            [DayOfWeek.Sunday] = "Domingo",
            [DayOfWeek.Monday] = "Segunda-feira",
            [DayOfWeek.Tuesday] = "Terca-feira",
            [DayOfWeek.Wednesday] = "Quarta-feira",
            [DayOfWeek.Thursday] = "Quinta-feira",
            [DayOfWeek.Friday] = "Sexta-feira",
            [DayOfWeek.Saturday] = "Sabado"
        };

        return scores
            .Where(s => s.FocusScore < 40 || s.ProductivityRatio < 0.3)
            .Select(s =>
            {
                var reason = s.FocusScore < 20 ? "Dia muito desafiador com foco criticamente baixo"
                    : s.DistractionSeconds > s.ProductiveSeconds ? "Tempo de distracao superou o tempo produtivo"
                    : s.ContextSwitchesCount > 30 ? "Muitas trocas de contexto reduziram a produtividade"
                    : "Desempenho abaixo do esperado";

                return new UnproductiveDay
                {
                    DayName = dayNames.GetValueOrDefault(s.Date.DayOfWeek, s.Date.ToString("dd/MM")),
                    FocusScore = s.FocusScore,
                    Reason = reason
                };
            })
            .ToList();
    }

    private async Task<List<string>> GetTopAppsAsync(
        Guid userId, DateOnly weekStart, DateOnly weekEnd, CancellationToken ct)
    {
        var weekStartUtc = DateTime.SpecifyKind(weekStart.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var weekEndUtc = DateTime.SpecifyKind(weekEnd.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

        return await _context.ActivitySessions
            .IgnoreQueryFilters()
            .Where(a => a.UserId == userId && a.StartedAt >= weekStartUtc && a.StartedAt <= weekEndUtc)
            .GroupBy(a => a.ProcessName)
            .OrderByDescending(g => g.Sum(s => s.DurationSeconds))
            .Select(g => g.Key)
            .Take(5)
            .ToListAsync(ct);
    }
}
