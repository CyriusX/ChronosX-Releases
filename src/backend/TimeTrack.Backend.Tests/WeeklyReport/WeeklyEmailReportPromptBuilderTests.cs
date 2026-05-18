using System.Text.Json;
using FluentAssertions;
using TimeTrack.Backend.AI.Interfaces;
using TimeTrack.Backend.AI.Services;
using Xunit;

namespace TimeTrack.Backend.Tests.WeeklyReport;

public sealed class WeeklyEmailReportPromptBuilderTests
{
    [Fact]
    public void BuildWeeklyEmailReportPrompt_WithFullContext_ReturnsValidPrompts()
    {
        var context = new WeeklyEmailReportContext
        {
            UserName = "Carlos",
            Period = "05/05/2026 a 11/05/2026",
            CurrentWeek = new UserWeekMetrics
            {
                TotalActiveHours = 42.5,
                AvgFocusScore = 78.05,
                ProductiveRatio = 0.72,
                ContextSwitches = 25,
                InterruptionCount = 8
            },
            PreviousWeek = new UserWeekMetrics
            {
                TotalActiveHours = 42.45,
                AvgFocusScore = 78.0,
                ProductiveRatio = 0.71,
                ContextSwitches = 30,
                InterruptionCount = 12
            },
            TeamRankings =
            [
                new TeamMemberRanking { Name = "Ana", FocusScore = 85.0, Position = 1 },
                new TeamMemberRanking { Name = "Carlos", FocusScore = 78.3, Position = 2 },
                new TeamMemberRanking { Name = "Bruno", FocusScore = 65.0, Position = 3 }
            ],
            TopDifficulties =
            [
                new DifficultyItem { AppName = "youtube", Hours = 3.5, Category = "streaming" },
                new DifficultyItem { AppName = "whatsapp", Hours = 1.2, Category = "communication" }
            ],
            UnproductiveDays =
            [
                new UnproductiveDay { DayName = "Quarta-feira", FocusScore = 15, Reason = "Tempo de distracao superou o tempo produtivo" }
            ],
            TopApps = ["code", "chrome", "slack"],
            Preferences = new ReportPreferences
            {
                IncludeTeamComparison = true,
                IncludeDifficultyAnalysis = true,
                IncludeWeekOverWeek = true,
                IncludeUnproductiveDays = true
            }
        };

        var (systemPrompt, userPrompt) = PromptBuilder.BuildWeeklyEmailReportPrompt(context);

        systemPrompt.Should().Contain("productivity coach");
        systemPrompt.Should().Contain("pt-BR");
        systemPrompt.Should().Contain("Carlos");
        systemPrompt.Should().Contain("NEVER show raw numbers");
        systemPrompt.Should().Contain("Resumo da Semana");

        var parsed = JsonDocument.Parse(userPrompt);
        parsed.RootElement.GetProperty("user_name").GetString().Should().Be("Carlos");
        parsed.RootElement.GetProperty("period").GetString().Should().Be("05/05/2026 a 11/05/2026");

        var currentWeek = parsed.RootElement.GetProperty("current_week");
        currentWeek.GetProperty("total_active_hours").GetDouble().Should().BeApproximately(42.5, 0.01);
        currentWeek.GetProperty("avg_focus_level").GetString().Should().Be("good");
        currentWeek.GetProperty("productive_ratio_level").GetString().Should().Be("mostly_productive");

        var prevWeek = parsed.RootElement.GetProperty("previous_week");
        prevWeek.GetProperty("focus_trend").GetString().Should().NotBeNullOrEmpty();
        prevWeek.GetProperty("productivity_trend").GetString().Should().NotBeNullOrEmpty();
        prevWeek.GetProperty("hours_trend").GetString().Should().NotBeNullOrEmpty();

        parsed.RootElement.TryGetProperty("team_comparison", out var team).Should().BeTrue();
        team.GetArrayLength().Should().Be(3);

        parsed.RootElement.TryGetProperty("top_difficulties", out var diffs).Should().BeTrue();
        diffs.GetArrayLength().Should().Be(2);

        parsed.RootElement.TryGetProperty("unproductive_days", out var unprod).Should().BeTrue();
        unprod.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public void BuildWeeklyEmailReportPrompt_WithMinimalContext_ReturnsValidPrompts()
    {
        var context = new WeeklyEmailReportContext
        {
            UserName = "Maria",
            Period = "05/05/2026 a 11/05/2026",
            CurrentWeek = new UserWeekMetrics
            {
                TotalActiveHours = 20.0,
                AvgFocusScore = 45.0,
                ProductiveRatio = 0.4,
                ContextSwitches = 50,
                InterruptionCount = 20
            },
            Preferences = new ReportPreferences
            {
                IncludeTeamComparison = false,
                IncludeDifficultyAnalysis = false,
                IncludeWeekOverWeek = false,
                IncludeUnproductiveDays = false
            }
        };

        var (_, userPrompt) = PromptBuilder.BuildWeeklyEmailReportPrompt(context);

        var parsed = JsonDocument.Parse(userPrompt);
        parsed.RootElement.GetProperty("user_name").GetString().Should().Be("Maria");

        var currentWeek = parsed.RootElement.GetProperty("current_week");
        currentWeek.GetProperty("avg_focus_level").GetString().Should().Be("moderate");
        currentWeek.GetProperty("productive_ratio_level").GetString().Should().Be("balanced");
        currentWeek.GetProperty("context_switches_level").GetString().Should().Be("high");
        currentWeek.GetProperty("interruptions_level").GetString().Should().Be("frequent");

        // When disabled, these sections should NOT be present
        parsed.RootElement.TryGetProperty("team_comparison", out _).Should().BeFalse();
        parsed.RootElement.TryGetProperty("top_difficulties", out _).Should().BeFalse();
        parsed.RootElement.TryGetProperty("unproductive_days", out _).Should().BeFalse();
        parsed.RootElement.TryGetProperty("previous_week", out _).Should().BeFalse();

        // requested_sections should only have enabled sections (all disabled = empty)
        var sections = parsed.RootElement.GetProperty("requested_sections");
        sections.GetArrayLength().Should().Be(0);
    }

    [Theory]
    [InlineData(90.0, "excellent")]
    [InlineData(80.0, "excellent")]
    [InlineData(70.0, "good")]
    [InlineData(60.0, "good")]
    [InlineData(50.0, "moderate")]
    [InlineData(40.0, "moderate")]
    [InlineData(30.0, "below_average")]
    [InlineData(20.0, "below_average")]
    [InlineData(10.0, "challenging")]
    [InlineData(0.0, "challenging")]
    public void BuildWeeklyEmailReportPrompt_ScoreToLabel_MapsCorrectly(double score, string expectedLabel)
    {
        var context = new WeeklyEmailReportContext
        {
            UserName = "Test",
            Period = "test",
            CurrentWeek = new UserWeekMetrics { AvgFocusScore = score },
            Preferences = new ReportPreferences()
        };

        var (_, userPrompt) = PromptBuilder.BuildWeeklyEmailReportPrompt(context);

        var parsed = JsonDocument.Parse(userPrompt);
        parsed.RootElement.GetProperty("current_week").GetProperty("avg_focus_level").GetString().Should().Be(expectedLabel);
    }

    [Theory]
    [InlineData(0.9, "highly_productive")]
    [InlineData(0.8, "highly_productive")]
    [InlineData(0.7, "mostly_productive")]
    [InlineData(0.6, "mostly_productive")]
    [InlineData(0.5, "balanced")]
    [InlineData(0.4, "balanced")]
    [InlineData(0.3, "mostly_distracted")]
    [InlineData(0.2, "mostly_distracted")]
    [InlineData(0.1, "highly_distracted")]
    public void BuildWeeklyEmailReportPrompt_RatioToLabel_MapsCorrectly(double ratio, string expectedLabel)
    {
        var context = new WeeklyEmailReportContext
        {
            UserName = "Test",
            Period = "test",
            CurrentWeek = new UserWeekMetrics { ProductiveRatio = ratio },
            Preferences = new ReportPreferences()
        };

        var (_, userPrompt) = PromptBuilder.BuildWeeklyEmailReportPrompt(context);

        var parsed = JsonDocument.Parse(userPrompt);
        parsed.RootElement.GetProperty("current_week").GetProperty("productive_ratio_level").GetString().Should().Be(expectedLabel);
    }

    [Fact]
    public void BuildWeeklyEmailReportPrompt_RequestedSections_ReflectsPreferences()
    {
        var context = new WeeklyEmailReportContext
        {
            UserName = "Test",
            Period = "test",
            CurrentWeek = new UserWeekMetrics(),
            TeamRankings = [new TeamMemberRanking { Name = "A", FocusScore = 80, Position = 1 }],
            TopDifficulties = [new DifficultyItem { AppName = "x", Hours = 1, Category = "social" }],
            UnproductiveDays = [new UnproductiveDay { DayName = "Mon", FocusScore = 10, Reason = "bad" }],
            PreviousWeek = new UserWeekMetrics(),
            Preferences = new ReportPreferences
            {
                IncludeTeamComparison = true,
                IncludeDifficultyAnalysis = false,
                IncludeWeekOverWeek = true,
                IncludeUnproductiveDays = false
            }
        };

        var (_, userPrompt) = PromptBuilder.BuildWeeklyEmailReportPrompt(context);

        var parsed = JsonDocument.Parse(userPrompt);
        var sections = parsed.RootElement.GetProperty("requested_sections");
        sections.GetArrayLength().Should().Be(2);

        // Team comparison included because preference is true
        parsed.RootElement.TryGetProperty("team_comparison", out _).Should().BeTrue();
        // Difficulties NOT included because preference is false
        parsed.RootElement.TryGetProperty("top_difficulties", out _).Should().BeFalse();
        // Unproductive days NOT included because preference is false
        parsed.RootElement.TryGetProperty("unproductive_days", out _).Should().BeFalse();
        // Previous week included because IncludeWeekOverWeek is true
        parsed.RootElement.TryGetProperty("previous_week", out _).Should().BeTrue();
    }

    [Theory]
    [InlineData(75.05, 75.0, "slightly_improved")]
    [InlineData(75.0, 75.0, "stable")]
    [InlineData(74.9, 75.0, "slightly_declined")]
    [InlineData(50.0, 75.0, "significantly_declined")]
    [InlineData(90.0, 70.0, "strongly_improved")]
    public void BuildWeeklyEmailReportPrompt_TrendComparison_Works(
        double current, double previous, string expectedTrend)
    {
        var context = new WeeklyEmailReportContext
        {
            UserName = "Test",
            Period = "test",
            CurrentWeek = new UserWeekMetrics { AvgFocusScore = current },
            PreviousWeek = new UserWeekMetrics { AvgFocusScore = previous },
            Preferences = new ReportPreferences { IncludeWeekOverWeek = true }
        };

        var (_, userPrompt) = PromptBuilder.BuildWeeklyEmailReportPrompt(context);

        var parsed = JsonDocument.Parse(userPrompt);
        parsed.RootElement.GetProperty("previous_week").GetProperty("focus_trend").GetString().Should().Be(expectedTrend);
    }
}
