using System.Text.Json;
using FluentAssertions;
using TimeTrack.Backend.AI.Interfaces;
using TimeTrack.Backend.AI.Services;
using Xunit;

namespace TimeTrack.Backend.Tests.AI;

public sealed class PromptBuilderTests
{
    [Fact]
    public void BuildClassificationPrompt_WithoutFewShot_ReturnsValidPrompts()
    {
        var request = new AppClassificationRequest
        {
            ExeName = "code",
            SampleWindowTitles = ["Program.cs - Visual Studio Code"],
            AvgDailySeconds = 14400,
            UsageDaysLast30 = 22,
            CorrelationWithFocusScore = 0.85,
            CoOccurringApps = ["chrome", "terminal"],
        };

        var (systemPrompt, userPrompt) = PromptBuilder.BuildClassificationPrompt(request);

        systemPrompt.Should().Contain("app usage classifier");
        systemPrompt.Should().Contain("category");

        var parsed = JsonDocument.Parse(userPrompt);
        parsed.RootElement.GetProperty("exe_name").GetString().Should().Be("code");
        parsed.RootElement.GetProperty("avg_daily_seconds").GetInt32().Should().Be(14400);
        parsed.RootElement.GetProperty("usage_days_last_30").GetInt32().Should().Be(22);
        parsed.RootElement.GetProperty("correlation_with_focus_score").GetDouble().Should().BeApproximately(0.85, 0.001);
        // When FewShotExamples is null/empty, org_corrections is serialized as null
        parsed.RootElement.TryGetProperty("org_corrections", out var corrections).Should().BeTrue();
        corrections.ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void BuildClassificationPrompt_WithFewShot_IncludesCorrections()
    {
        var request = new AppClassificationRequest
        {
            ExeName = "spotify",
            FewShotExamples =
            [
                new FeedbackExample { ExeName = "vlc", CorrectCategory = "distracting", Reason = "Media player" }
            ],
        };

        var (_, userPrompt) = PromptBuilder.BuildClassificationPrompt(request);

        var parsed = JsonDocument.Parse(userPrompt);
        var corrections = parsed.RootElement.GetProperty("org_corrections");
        corrections.GetArrayLength().Should().Be(1);
        corrections[0].GetProperty("exe_name").GetString().Should().Be("vlc");
        corrections[0].GetProperty("correct_category").GetString().Should().Be("distracting");
    }

    [Fact]
    public void BuildWeeklyNarrativePrompt_WithContext_ReturnsValidPrompts()
    {
        var context = new WeeklyFeatureContext
        {
            Period = "01/05/2026 a 07/05/2026",
            Features = new WeeklyFeatures
            {
                TotalActiveHours = 42.5,
                AvgFocusScore = 78.3,
                AvgProductiveRatio = 0.72,
                TrendFocusScore = 5.2,
            },
            Patterns = [new PatternSummary { Tag = "morning_productive", Strength = 0.85 }],
            Anomalies = [],
            UserContext = new UserAiContext
            {
                TopApps = ["code", "chrome"],
                TeamAvgFocusScore = 72.1,
                TeamSize = 5,
            },
        };

        var (systemPrompt, userPrompt) = PromptBuilder.BuildWeeklyNarrativePrompt(context);

        systemPrompt.Should().Contain("productivity coach");
        systemPrompt.Should().Contain("pt-BR");
        systemPrompt.Should().Contain("profissional e encorajador");

        var parsed = JsonDocument.Parse(userPrompt);
        parsed.RootElement.GetProperty("period").GetString().Should().Be("01/05/2026 a 07/05/2026");

        var features = parsed.RootElement.GetProperty("features");
        features.GetProperty("total_active_hours").GetDouble().Should().BeApproximately(42.5, 0.01);

        parsed.RootElement.TryGetProperty("top_apps_this_week", out var apps).Should().BeTrue();
        apps.GetArrayLength().Should().Be(2);
        parsed.RootElement.GetProperty("team_avg_focus_score").GetDouble().Should().BeApproximately(72.1, 0.01);
        parsed.RootElement.GetProperty("team_size").GetInt32().Should().Be(5);
    }

    [Fact]
    public void BuildWeeklyNarrativePrompt_WithoutUserContext_OmitsOptionalFields()
    {
        var context = new WeeklyFeatureContext
        {
            Period = "01/05/2026 a 07/05/2026",
            Features = new WeeklyFeatures { TotalActiveHours = 30 },
            Patterns = [],
            Anomalies = [],
        };

        var (_, userPrompt) = PromptBuilder.BuildWeeklyNarrativePrompt(context);

        var parsed = JsonDocument.Parse(userPrompt);
        parsed.RootElement.TryGetProperty("top_apps_this_week", out _).Should().BeFalse();
        parsed.RootElement.TryGetProperty("team_avg_focus_score", out _).Should().BeFalse();
    }

    [Fact]
    public void BuildPatternDescriptionPrompt_ReturnsValidPrompts()
    {
        var pattern = new UserPatternContext
        {
            PatternTag = "high_context_switching",
            Strength = 0.92,
            Evidence = "Average 45 switches per day",
            Language = "en-US",
        };

        var (systemPrompt, userPrompt) = PromptBuilder.BuildPatternDescriptionPrompt(pattern);

        systemPrompt.Should().Contain("behavioral analyst");
        systemPrompt.Should().Contain("en-US");

        var parsed = JsonDocument.Parse(userPrompt);
        parsed.RootElement.GetProperty("pattern_tag").GetString().Should().Be("high_context_switching");
        parsed.RootElement.GetProperty("strength").GetDouble().Should().BeApproximately(0.92, 0.001);
    }

    [Fact]
    public void BuildAlertMessagePrompt_ReturnsValidPrompts()
    {
        var context = new AlertContext
        {
            AlertType = "overwork",
            Severity = "warning",
            Data = new Dictionary<string, object> { ["hours"] = 12 },
            Language = "pt-BR",
        };

        var (systemPrompt, userPrompt) = PromptBuilder.BuildAlertMessagePrompt(context);

        systemPrompt.Should().Contain("productivity assistant");
        systemPrompt.Should().Contain("warning");

        var parsed = JsonDocument.Parse(userPrompt);
        parsed.RootElement.GetProperty("alert_type").GetString().Should().Be("overwork");
        parsed.RootElement.GetProperty("severity").GetString().Should().Be("warning");
    }

    [Fact]
    public void BuildReportsSuggestionPrompt_WithAllFields_ReturnsValidPrompts()
    {
        var context = new ReportsSuggestionContext
        {
            FocusScore = 65.2,
            Trend = "declining",
            BaselineFocus = 78.0,
            FocusDiff = -12.8,
            AvgContextSwitches = 32.5,
            Patterns = [new PatternSummary { Tag = "afternoon_focus_drop", Strength = 0.78 }],
            Anomalies = [new AnomalySummary { Type = "productivity_drop", Severity = "warning" }],
            UserContext = new UserAiContext { TeamAvgFocusScore = 71.0, TeamSize = 8 },
            TopDistractions = ["youtube", "reddit"],
        };

        var (systemPrompt, userPrompt) = PromptBuilder.BuildReportsSuggestionPrompt(context);

        systemPrompt.Should().Contain("productivity coach");
        systemPrompt.Should().Contain("actionable");

        var parsed = JsonDocument.Parse(userPrompt);
        parsed.RootElement.GetProperty("focus_score").GetDouble().Should().BeApproximately(65.2, 0.01);
        parsed.RootElement.GetProperty("trend").GetString().Should().Be("declining");
        parsed.RootElement.GetProperty("baseline_focus").GetDouble().Should().BeApproximately(78.0, 0.01);
        parsed.RootElement.GetProperty("focus_diff").GetDouble().Should().BeApproximately(-12.8, 0.01);
        parsed.RootElement.GetProperty("team_avg_focus_score").GetDouble().Should().BeApproximately(71.0, 0.01);
        parsed.RootElement.GetProperty("team_size").GetInt32().Should().Be(8);
    }

    [Fact]
    public void BuildReportsSuggestionPrompt_WithoutOptionalFields_OmitsThem()
    {
        var context = new ReportsSuggestionContext
        {
            FocusScore = 50.0,
            Trend = "stable",
            AvgContextSwitches = 10,
            Patterns = [],
            Anomalies = [],
        };

        var (_, userPrompt) = PromptBuilder.BuildReportsSuggestionPrompt(context);

        var parsed = JsonDocument.Parse(userPrompt);
        parsed.RootElement.TryGetProperty("team_avg_focus_score", out _).Should().BeFalse();
        parsed.RootElement.TryGetProperty("top_distractions", out _).Should().BeFalse();
    }
}
