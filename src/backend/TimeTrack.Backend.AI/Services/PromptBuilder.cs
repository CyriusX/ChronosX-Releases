using System.Text.Json;
using TimeTrack.Backend.AI.Interfaces;

namespace TimeTrack.Backend.AI.Services;

public static class PromptBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static (string SystemPrompt, string UserPrompt) BuildClassificationPrompt(AppClassificationRequest request)
    {
        var systemPrompt =
            "You are an app usage classifier for a productivity tracking tool. " +
            "Given the app name, window titles, usage patterns, and correlation data, classify the app. " +
            "Respond ONLY with valid JSON: " +
            "{\"category\": \"productive\" | \"neutral\" | \"distracting\", " +
            "\"subcategory\": \"<ide, communication, social_media, streaming, etc.>\", " +
            "\"confidence\": <0.0-1.0>, " +
            "\"reasoning\": \"<brief explanation>\"}";

        var userPromptObj = (object)new
        {
            exe_name = request.ExeName,
            sample_window_titles = request.SampleWindowTitles,
            avg_daily_seconds = request.AvgDailySeconds,
            usage_days_last_30 = request.UsageDaysLast30,
            usage_hours = request.UsageHours,
            correlation_with_focus_score = request.CorrelationWithFocusScore,
            co_occurring_apps = request.CoOccurringApps,
            org_corrections = request.FewShotExamples?.Count > 0
                ? request.FewShotExamples.Select(f => new
                {
                    exe_name = f.ExeName,
                    correct_category = f.CorrectCategory,
                    reason = f.Reason ?? "Admin correction"
                })
                : (object?)null
        };

        return (systemPrompt, JsonSerializer.Serialize(userPromptObj, JsonOptions));
    }

    public static (string SystemPrompt, string UserPrompt) BuildWeeklyNarrativePrompt(WeeklyFeatureContext context)
    {
        var ctx = context.UserContext;
        var systemPrompt =
            $"You are a productivity coach writing a weekly summary for a user. " +
            $"Language: {context.Language}. Tone: {context.Tone}. " +
            $"Use at most {context.MaxSentences} sentences. Be specific with numbers. " +
            "Respond with plain text only - no JSON, no markdown.";

        var userPromptObj = new Dictionary<string, object?>
        {
            ["period"] = context.Period,
            ["features"] = new
            {
                total_active_hours = Math.Round(context.Features.TotalActiveHours, 1),
                avg_focus_score = Math.Round(context.Features.AvgFocusScore, 1),
                avg_productive_ratio = Math.Round(context.Features.AvgProductiveRatio, 3),
                trend_focus_score = context.Features.TrendFocusScore,
                trend_productive_ratio = context.Features.TrendProductiveRatio,
            },
            ["patterns"] = context.Patterns,
            ["anomalies"] = context.Anomalies,
        };

        if (ctx is not null)
        {
            if (ctx.TopApps.Count > 0)
                userPromptObj["top_apps_this_week"] = ctx.TopApps;
            if (ctx.TeamAvgFocusScore.HasValue)
                userPromptObj["team_avg_focus_score"] = Math.Round(ctx.TeamAvgFocusScore.Value, 1);
            if (ctx.PreviousPeriodFocus.HasValue)
                userPromptObj["previous_week_avg_focus"] = Math.Round(ctx.PreviousPeriodFocus.Value, 1);
            if (ctx.TeamSize > 0)
                userPromptObj["team_size"] = ctx.TeamSize;
        }

        return (systemPrompt, JsonSerializer.Serialize(userPromptObj, JsonOptions));
    }

    public static (string SystemPrompt, string UserPrompt) BuildPatternDescriptionPrompt(UserPatternContext pattern)
    {
        var ctx = pattern.UserContext;
        var systemPrompt =
            $"You are a behavioral analyst describing a detected user productivity pattern. " +
            $"Language: {pattern.Language}. Write a concise 1-2 sentence description of what this pattern means. " +
            "Respond with plain text only.";

        var userPromptObj = new Dictionary<string, object?>
        {
            ["pattern_tag"] = pattern.PatternTag,
            ["strength"] = pattern.Strength,
            ["evidence"] = pattern.Evidence,
        };

        if (ctx is not null)
        {
            if (ctx.TopApps.Count > 0)
                userPromptObj["user_top_apps"] = ctx.TopApps;
            if (ctx.TeamAvgFocusScore.HasValue)
                userPromptObj["team_avg_focus_score"] = Math.Round(ctx.TeamAvgFocusScore.Value, 1);
        }

        return (systemPrompt, JsonSerializer.Serialize(userPromptObj, JsonOptions));
    }

    public static (string SystemPrompt, string UserPrompt) BuildAlertMessagePrompt(AlertContext context)
    {
        var ctx = context.UserContext;
        var systemPrompt =
            $"You are a productivity assistant generating a short alert message. " +
            $"Language: {context.Language}. Severity: {context.Severity}. " +
            "Write a single concise sentence that is actionable and empathetic. " +
            "Use the quantitative data provided to make the message specific and useful. " +
            "Respond with plain text only. Do not be generic.";

        var userPromptObj = new Dictionary<string, object?>
        {
            ["alert_type"] = context.AlertType,
            ["severity"] = context.Severity,
            ["data"] = context.Data,
        };

        if (ctx is not null)
        {
            if (ctx.TopApps.Count > 0)
                userPromptObj["user_top_apps"] = ctx.TopApps;
            if (ctx.PreviousPeriodFocus.HasValue)
                userPromptObj["recent_focus_trend"] = Math.Round(ctx.PreviousPeriodFocus.Value, 1);
        }

        return (systemPrompt, JsonSerializer.Serialize(userPromptObj, JsonOptions));
    }

    public static (string SystemPrompt, string UserPrompt) BuildReportsSuggestionPrompt(ReportsSuggestionContext context)
    {
        var ctx = context.UserContext;
        var systemPrompt =
            $"You are a productivity coach generating an actionable suggestion for a user. " +
            $"Language: {context.Language}. " +
            "Write 1-2 concise sentences with a specific, actionable recommendation based on the data provided. " +
            "Use the quantitative data to make the suggestion specific and useful. " +
            "Do not be generic. Respond with plain text only.";

        var userPromptObj = new Dictionary<string, object?>
        {
            ["focus_score"] = Math.Round(context.FocusScore, 1),
            ["trend"] = context.Trend,
            ["baseline_focus"] = context.BaselineFocus.HasValue ? Math.Round(context.BaselineFocus.Value, 1) : (double?)null,
            ["focus_diff"] = context.FocusDiff.HasValue ? Math.Round(context.FocusDiff.Value, 1) : (double?)null,
            ["avg_context_switches"] = Math.Round(context.AvgContextSwitches, 1),
            ["patterns"] = context.Patterns.Select(p => new { p.Tag, Strength = Math.Round(p.Strength, 2) }),
            ["anomalies"] = context.Anomalies.Select(a => new { a.Type, a.Severity }),
        };

        if (ctx is not null)
        {
            if (ctx.TeamAvgFocusScore.HasValue)
                userPromptObj["team_avg_focus_score"] = Math.Round(ctx.TeamAvgFocusScore.Value, 1);
            if (ctx.TeamSize > 0)
                userPromptObj["team_size"] = ctx.TeamSize;
        }

        if (context.TopDistractions is { Count: > 0 })
            userPromptObj["top_distractions"] = context.TopDistractions;

        return (systemPrompt, JsonSerializer.Serialize(userPromptObj, JsonOptions));
    }

    public static (string SystemPrompt, string UserPrompt) BuildWeeklyEmailReportPrompt(WeeklyEmailReportContext context)
    {
        var prefs = context.Preferences;
        var sections = new List<string>();
        if (prefs.IncludeTeamComparison) sections.Add("team_comparison");
        if (prefs.IncludeDifficultyAnalysis) sections.Add("difficulty_analysis");
        if (prefs.IncludeWeekOverWeek) sections.Add("week_over_week");
        if (prefs.IncludeUnproductiveDays) sections.Add("unproductive_days");

        var systemPrompt =
            $"You are a friendly productivity coach writing a personalized weekly email report. " +
            $"Language: {context.Language}. " +
            "You MUST follow these rules:\n" +
            "1. NEVER show raw numbers, percentages, or data tables. Instead use qualitative language (e.g., 'well above average', 'slightly below your usual', 'a challenging day').\n" +
            "2. Be conversational, empathetic, and actionable.\n" +
            "3. Structure your response with EXACTLY these section headers in this order (skip sections not listed):\n" +
            "   ## Resumo da Semana\n" +
            "   ## Sua Posicao na Equipe\n" +
            "   ## Desafios Encontrados\n" +
            "   ## Evolucao Semanal\n" +
            "   ## Dias a Observar\n" +
            "   ## Recomendacao para a Proxima Semana\n" +
            "4. Each section should be 2-3 short paragraphs maximum.\n" +
            "5. End with ONE specific, actionable recommendation.\n" +
            "6. Respond with plain text only - no JSON, no markdown formatting besides the headers.\n" +
            $"7. Address the user by name: {context.UserName}.";

        var userPromptObj = new Dictionary<string, object?>
        {
            ["user_name"] = context.UserName,
            ["period"] = context.Period,
            ["requested_sections"] = sections,
            ["current_week"] = new
            {
                total_active_hours = Math.Round(context.CurrentWeek.TotalActiveHours, 1),
                avg_focus_level = ScoreToLabel(context.CurrentWeek.AvgFocusScore),
                productive_ratio_level = RatioToLabel(context.CurrentWeek.ProductiveRatio),
                context_switches_level = context.CurrentWeek.ContextSwitches > 30 ? "high" : context.CurrentWeek.ContextSwitches > 15 ? "moderate" : "low",
                interruptions_level = context.CurrentWeek.InterruptionCount > 10 ? "frequent" : context.CurrentWeek.InterruptionCount > 5 ? "occasional" : "few"
            }
        };

        if (context.PreviousWeek is not null)
        {
            userPromptObj["previous_week"] = new
            {
                focus_trend = CompareTrend(context.CurrentWeek.AvgFocusScore, context.PreviousWeek.AvgFocusScore),
                productivity_trend = CompareTrend(context.CurrentWeek.ProductiveRatio, context.PreviousWeek.ProductiveRatio),
                hours_trend = CompareTrend(context.CurrentWeek.TotalActiveHours, context.PreviousWeek.TotalActiveHours)
            };
        }

        if (context.TeamRankings.Count > 0 && prefs.IncludeTeamComparison)
        {
            userPromptObj["team_comparison"] = context.TeamRankings.Select(r => new
            {
                r.Name,
                performance = ScoreToLabel(r.FocusScore),
                r.Position
            });
        }

        if (context.TopDifficulties.Count > 0 && prefs.IncludeDifficultyAnalysis)
        {
            userPromptObj["top_difficulties"] = context.TopDifficulties.Select(d => new
            {
                d.AppName,
                impact = d.Hours > 5 ? "significant" : d.Hours > 2 ? "moderate" : "minor",
                d.Category
            });
        }

        if (context.UnproductiveDays.Count > 0 && prefs.IncludeUnproductiveDays)
        {
            userPromptObj["unproductive_days"] = context.UnproductiveDays.Select(d => new
            {
                d.DayName,
                performance = ScoreToLabel(d.FocusScore),
                d.Reason
            });
        }

        if (context.TopApps.Count > 0)
            userPromptObj["top_apps"] = context.TopApps.Take(5);

        return (systemPrompt, JsonSerializer.Serialize(userPromptObj, JsonOptions));
    }

    private static string ScoreToLabel(double score) => score switch
    {
        >= 80 => "excellent",
        >= 60 => "good",
        >= 40 => "moderate",
        >= 20 => "below_average",
        _ => "challenging"
    };

    private static string RatioToLabel(double ratio) => ratio switch
    {
        >= 0.8 => "highly_productive",
        >= 0.6 => "mostly_productive",
        >= 0.4 => "balanced",
        >= 0.2 => "mostly_distracted",
        _ => "highly_distracted"
    };

    private static string CompareTrend(double current, double previous)
    {
        var diff = current - previous;
        if (diff > 0.1) return "strongly_improved";
        if (diff > 0.02) return "slightly_improved";
        if (diff > -0.02) return "stable";
        if (diff > -0.1) return "slightly_declined";
        return "significantly_declined";
    }
}
