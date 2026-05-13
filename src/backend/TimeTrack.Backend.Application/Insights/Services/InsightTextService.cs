using TimeTrack.Backend.Application.Insights.DTOs;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.Insights.Services;

public static class InsightTextService
{
    public static string GenerateSuggestion(
        DailyFocusScore today, double? avgFocus, double? focusDiff,
        List<string> patternTags)
    {
        if (today.FocusScore >= 80)
            return "Dia excelente de foco! Mantenha o ritmo.";
        if (today.FocusScore < 40)
            return "Dia difícil. Que tal uma pausa curta e uma sessão de foco de 25 min?";

        var suggestions = new List<string>();

        if (today.ContextSwitchesCount > 30)
            suggestions.Add("Muitas alternâncias de contexto. Tente agrupar tarefas similares.");
        if (today.DistractionSeconds > today.ProductiveSeconds * 0.3)
            suggestions.Add("Tempo de distração alto. Considere bloquear apps de distração.");
        if (today.LongestFocusSeconds > 5400)
            suggestions.Add("Ótimo: você teve um bloco de foco profundo de mais de 90 minutos!");
        if (focusDiff < -15)
            suggestions.Add("Seu foco está significativamente abaixo da sua média pessoal.");
        if (patternTags.Contains("morning_productive"))
            suggestions.Add("Você rende mais pela manhã. Agende tarefas importantes antes do meio-dia.");

        return suggestions.FirstOrDefault() ?? "Continue acompanhando seus padrões de foco.";
    }

    public static string BuildInsightText(
        DailyFocusScore today, double? focusDiff, double? avgFocus)
    {
        var hours = today.ProductiveSeconds / 3600.0;
        var text = $"Você teve {hours:F1}h produtivas hoje com foco {today.FocusScore}/100.";

        if (avgFocus.HasValue)
        {
            var diff = focusDiff ?? 0;
            if (diff > 5)
                text += $" Seu foco está {Math.Abs(diff):F0} pontos acima da sua média.";
            else if (diff < -5)
                text += $" Seu foco está {Math.Abs(diff):F0} pontos abaixo da sua média.";
            else
                text += " Seu foco está na média pessoal.";
        }

        return text;
    }

    public static List<LiveInsightItem> GenerateLiveInsights(
        long distractionMs, long productiveMs, long totalMs,
        long longestFocusBlockMs, int contextSwitches,
        Dictionary<string, long> distractionByApp)
    {
        var insights = new List<LiveInsightItem>();
        if (totalMs == 0) return insights;

        foreach (var (appName, appMs) in distractionByApp
            .OrderByDescending(kvp => kvp.Value)
            .Take(2))
        {
            var appHours = appMs / 3_600_000.0;
            if (appHours >= 0.1)
            {
                var formatted = FormatAppName(appName);
                var pct = Math.Round((double)appMs / totalMs * 100);
                var timeStr = FormatHours(appHours);
                insights.Add(new LiveInsightItem
                {
                    Type = "distraction_app",
                    Severity = "negative",
                    Message = pct > 10
                        ? $"Voce passou {timeStr} em {formatted} ({pct}% do tempo). Tente fechar esse app."
                        : $"Voce passou {timeStr} em {formatted}. Equilibre com tarefas produtivas."
                });
            }
        }

        if (longestFocusBlockMs > 25 * 60_000)
        {
            var focusHours = longestFocusBlockMs / 3_600_000.0;
            insights.Add(new LiveInsightItem
            {
                Type = "deep_focus",
                Severity = "positive",
                Message = $"Otimo: {FormatHours(focusHours)} de foco profundo sem interrupcao!"
            });
        }

        if (contextSwitches > 15)
        {
            insights.Add(new LiveInsightItem
            {
                Type = "context_switches",
                Severity = "negative",
                Message = $"{contextSwitches} trocas de contexto. Tente agrupar tarefas similares."
            });
        }

        var productiveHours = productiveMs / 3_600_000.0;
        var milestone = new[] { 6, 4, 2 }.FirstOrDefault(m => productiveHours >= m);
        if (milestone > 0)
        {
            insights.Add(new LiveInsightItem
            {
                Type = "milestone",
                Severity = "positive",
                Message = $"{milestone}h de trabalho produtivo hoje. Continue assim!"
            });
        }

        return insights;
    }

    public static AppProductivityCategory MapAppCategory(string? appCategory) =>
        appCategory?.ToLowerInvariant() switch
        {
            "productive" => AppProductivityCategory.Productive,
            "distraction" => AppProductivityCategory.Distraction,
            _ => AppProductivityCategory.Neutral,
        };

    public static string FormatAppName(string processName)
    {
        var name = Path.GetFileNameWithoutExtension(processName) ?? processName;
        return name.Replace(".", " ").Trim();
    }

    public static string FormatHours(double hours)
    {
        if (hours < 1)
        {
            var min = Math.Round(hours * 60);
            return $"{min}min";
        }
        var h = (int)hours;
        var remainingMin = (int)Math.Round((hours - h) * 60);
        return remainingMin > 0 ? $"{h}h{remainingMin}min" : $"{h}h";
    }

    public static string BuildDynamicSuggestion(
        double avgFocus, string trend, double avgContextSwitches,
        List<string> patternTags, int anomalyCount,
        List<string> topDistractions, List<string> topApps)
    {
        var parts = new List<string>();

        if (avgFocus < 50)
            parts.Add($"Foco de {Math.Round(avgFocus)}/100 esta baixo. Priorize blocos de foco de 25-50min sem interrupcoes.");
        else if (avgFocus > 75)
            parts.Add($"Excelente foco ({Math.Round(avgFocus)}/100). Continue mantendo os habitos atuais.");
        else
            parts.Add($"Foco de {Math.Round(avgFocus)}/100 com tendencia {trend}.");

        if (topDistractions.Count > 0)
            parts.Add($"Principais fontes de distração: {string.Join(", ", topDistractions.Take(3))}. Considere limitar o acesso durante horarios produtivos.");

        if (avgContextSwitches > 30)
            parts.Add($"{Math.Round(avgContextSwitches)} trocas de contexto/dia — acima do ideal. Tente agrupar tarefas similares.");
        else if (avgContextSwitches > 15)
            parts.Add($"{Math.Round(avgContextSwitches)} trocas de contexto/dia. Considere blocos de foco dedicados.");

        if (patternTags.Contains("morning_productive"))
            parts.Add("Rendimento maior pela manha: agende tarefas criticas antes do meio-dia.");
        if (patternTags.Contains("afternoon_focus_drop"))
            parts.Add("Queda de foco a tarde: faca uma pausa ativa apos o almoco.");
        if (patternTags.Contains("high_context_switching"))
            parts.Add("Padrao de trocas frequentes: considere metodos como Pomodoro.");
        if (patternTags.Contains("deep_work_capable"))
            parts.Add("Capacidade de foco profundo detectada: aproveite para tarefas complexas.");

        return string.Join(" ", parts);
    }

    public static DateOnly GetWeekStart(DateOnly date)
    {
        var dayOfWeek = (int)date.DayOfWeek;
        var monday = dayOfWeek == 0 ? -6 : 1 - dayOfWeek;
        return date.AddDays(monday);
    }
}
