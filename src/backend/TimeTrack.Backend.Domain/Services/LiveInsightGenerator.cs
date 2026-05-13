using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Domain.Services;

/// <summary>
/// Domain service for computing live productivity metrics and detecting
/// insight conditions from activity sessions. Pure computation — no DB or AI dependencies.
/// </summary>
public sealed class LiveInsightGenerator
{
    /// <summary>
    /// Computes aggregate live metrics from a list of activity sessions.
    /// </summary>
    public LiveMetrics ComputeMetrics(List<ActivitySession> sessions)
    {
        long productiveMs = 0, distractionMs = 0, neutralMs = 0;
        var contextSwitches = 0;
        var longestFocusBlockMs = 0L;
        var currentFocusBlockMs = 0L;
        string? lastProcess = null;

        var hourAgo = DateTime.UtcNow.AddHours(-1);
        var recentContextSwitches = 0;
        string? lastRecentProcess = null;

        var distractionByApp = new Dictionary<string, long>();

        foreach (var session in sessions.OrderBy(s => s.StartedAt))
        {
            var category = MapAppCategory(session.AppCategory);
            var durationMs = session.DurationSeconds * 1000L;

            switch (category)
            {
                case AppProductivityCategory.Productive:
                    productiveMs += durationMs;
                    currentFocusBlockMs += durationMs;
                    break;
                case AppProductivityCategory.Distraction:
                    distractionMs += durationMs;
                    if (currentFocusBlockMs > longestFocusBlockMs)
                        longestFocusBlockMs = currentFocusBlockMs;
                    currentFocusBlockMs = 0;
                    var appName = FormatAppName(session.ProcessName);
                    distractionByApp.TryGetValue(appName, out var existing);
                    distractionByApp[appName] = existing + durationMs;
                    break;
                default:
                    neutralMs += durationMs;
                    break;
            }

            if (lastProcess != null && lastProcess != session.ProcessName)
                contextSwitches++;

            lastProcess = session.ProcessName;

            if (session.StartedAt >= hourAgo)
            {
                if (lastRecentProcess != null && lastRecentProcess != session.ProcessName)
                    recentContextSwitches++;
                lastRecentProcess = session.ProcessName;
            }
        }

        if (currentFocusBlockMs > longestFocusBlockMs)
            longestFocusBlockMs = currentFocusBlockMs;

        var totalMs = productiveMs + distractionMs + neutralMs;

        return new LiveMetrics
        {
            ProductiveMs = productiveMs,
            DistractionMs = distractionMs,
            NeutralMs = neutralMs,
            TotalMs = totalMs,
            ContextSwitches = contextSwitches,
            RecentContextSwitches = recentContextSwitches,
            LongestFocusBlockMs = longestFocusBlockMs,
            TopDistractionApp = distractionByApp
                .OrderByDescending(kvp => kvp.Value).FirstOrDefault().Key,
            TopDistractionMs = distractionByApp.Count > 0
                ? distractionByApp.OrderByDescending(kvp => kvp.Value).First().Value : 0
        };
    }

    /// <summary>
    /// Detects live insight conditions from computed metrics.
    /// Returns a list of candidates ordered by priority.
    /// </summary>
    public List<LiveInsightCandidate> DetectConditions(
        Guid userId,
        Guid orgId,
        LiveMetrics metrics,
        LiveInsightThresholds thresholds)
    {
        var candidates = new List<LiveInsightCandidate>();

        if (metrics.TotalMs == 0) return candidates;

        var distractionRatio = (double)metrics.DistractionMs / metrics.TotalMs;
        var activeMinutes = metrics.TotalMs / 60_000.0;
        var productiveHours = metrics.ProductiveMs / 3_600_000.0;
        var distractionMinutes = metrics.DistractionMs / 60_000.0;
        var topAppMin = metrics.TopDistractionMs / 60_000.0;

        // Distraction alert
        if (distractionRatio > thresholds.DistractionThreshold)
        {
            var appHint = metrics.TopDistractionApp != null && topAppMin > 5
                ? $" Voce passou {Math.Round(topAppMin)} min em {metrics.TopDistractionApp}."
                : "";
            var msgs = new[]
            {
                $"Seu tempo de distracao esta em {Math.Round(distractionRatio * 100)}%.{appHint} Tente retomar o foco nas proximas tarefas.",
                $"Voce tem {Math.Round(distractionRatio * 100)}% do tempo em apps de distracao.{appHint} Que tal fechar esses apps por um tempo?",
                $"Alerta: {Math.Round(distractionRatio * 100)}% de distracao.{appHint} Priorize suas tarefas mais importantes agora.",
            };
            candidates.Add(new LiveInsightCandidate(
                userId, orgId, "live_distraction", "warning",
                msgs[Random.Shared.Next(msgs.Length)], distractionRatio));
        }

        // Overwork alert
        if (activeMinutes > thresholds.OverworkMinutesThreshold)
        {
            var msgs = new[]
            {
                $"Voce esta trabalhando ha {Math.Round(activeMinutes / 60.0)}h. Uma pausa de 10 min pode aumentar sua produtividade.",
                $"Ja se passaram {Math.Round(activeMinutes / 60.0)}h de trabalho. Levante, alongue e volte com mais energia.",
                $"{Math.Round(activeMinutes / 60.0)}h sem parar! Pesquisas mostram que pausas regulares melhoram o foco.",
            };
            candidates.Add(new LiveInsightCandidate(
                userId, orgId, "live_overwork", "info",
                msgs[Random.Shared.Next(msgs.Length)], activeMinutes / 120.0));
        }

        // High distraction time alert
        if (distractionMinutes > 30 && distractionRatio > 0.3)
        {
            var appHint = metrics.TopDistractionApp != null
                ? $" (maior parte em {metrics.TopDistractionApp})"
                : "";
            var msgs = new[]
            {
                $"Voce passou {Math.Round(distractionMinutes)} min em apps de distracao{appHint}. Tente equilibrar com tarefas produtivas.",
                $"{Math.Round(distractionMinutes)} min de distracao hoje{appHint} — {Math.Round(distractionRatio * 100)}% do seu tempo. Reduza para melhorar seu score.",
                $"Atencao: {Math.Round(distractionMinutes)} min em apps improdutivos{appHint}. Foque nas suas prioridades.",
            };
            candidates.Add(new LiveInsightCandidate(
                userId, orgId, "live_high_distraction", "warning",
                msgs[Random.Shared.Next(msgs.Length)], distractionRatio * 2));
        }

        // Deep focus streak (positive)
        if (metrics.LongestFocusBlockMs > thresholds.DeepFocusMinutesThreshold * 60_000L)
        {
            var focusMinutes = metrics.LongestFocusBlockMs / 60_000.0;
            var msgs = new[]
            {
                $"Excelente! Voce manteve foco profundo por {Math.Round(focusMinutes)} min seguidos. Continue nesse ritmo!",
                $"Bloco de foco de {Math.Round(focusMinutes)} min! Esse nivel de concentracao e raro — aproveite.",
                $"Impressionante: {Math.Round(focusMinutes)} min de foco ininterrupto. Voce esta no fluxo!",
            };
            candidates.Add(new LiveInsightCandidate(
                userId, orgId, "live_deep_focus", "info",
                msgs[Random.Shared.Next(msgs.Length)], 0.5));
        }

        // Milestone (positive)
        var milestones = new[] { 2, 4, 6 };
        foreach (var milestone in milestones)
        {
            if (productiveHours >= milestone && productiveHours < milestone + 0.1)
            {
                var msgs = new[]
                {
                    $"Parabens! {milestone}h de trabalho produtivo hoje. Continue assim!",
                    $"Marco atingido: {milestone}h produtivas! Voce esta tendo um dia muito focado.",
                    $"Incrivel! {milestone}h de produtividade. Seu esforco esta dando resultado.",
                };
                candidates.Add(new LiveInsightCandidate(
                    userId, orgId, "live_milestone", "info",
                    msgs[Random.Shared.Next(msgs.Length)], 0.3));
                break;
            }
        }

        return candidates;
    }

    private static AppProductivityCategory MapAppCategory(string? appCategory) =>
        appCategory?.ToLowerInvariant() switch
        {
            "productive" => AppProductivityCategory.Productive,
            "distraction" => AppProductivityCategory.Distraction,
            _ => AppProductivityCategory.Neutral,
        };

    internal static string FormatAppName(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return "App desconhecido";
        var name = processName.Split('-')[0].Split('–')[0].Trim();
        var suffixes = new[] { ".exe", " Browser", " - " };
        foreach (var s in suffixes) name = name.Replace(s, "", StringComparison.OrdinalIgnoreCase);
        return string.IsNullOrWhiteSpace(name) ? processName : name.Trim();
    }
}
