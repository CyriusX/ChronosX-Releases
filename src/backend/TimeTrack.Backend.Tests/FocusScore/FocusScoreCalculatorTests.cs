using FluentAssertions;
using TimeTrack.Backend.Application.FocusScore;
using TimeTrack.Backend.Domain.ValueObjects;
using Xunit;

namespace TimeTrack.Backend.Tests.FocusScore;

/// <summary>
/// Unit tests for FocusScoreCalculator
///
/// Test coverage:
/// - 100% focus (perfect day)
/// - 0% focus (no productive time)
/// - Maximum penalties
/// - Long focus block bonuses
/// - Edge cases (empty input, negative values)
/// </summary>
public sealed class FocusScoreCalculatorTests
{
    // ============================================================================
    // Perfect Score Tests
    // ============================================================================

    [Fact]
    public void Calculate_With100PercentFocus_Returns100()
    {
        // Arrange - 8 hours of pure productive time
        var input = FocusScoreInput.Create(
            totalTrackedMs: 8 * 60 * 60 * 1000, // 8 hours
            focusTimeMs: 8 * 60 * 60 * 1000,    // 8 hours productive
            distractionMs: 0,
            distractionCount: 0,
            pauseCount: 0,
            idleCount: 0,
            longFocusBlockCount: 10);

        // Act
        var score = FocusScoreCalculator.Calculate(input);

        // Assert
        score.Should().Be(100);
    }

    [Fact]
    public void Calculate_WithPerfectMetrics_ReturnsNear100()
    {
        // Arrange - Realistic perfect day with some idle time
        var input = FocusScoreInput.Create(
            totalTrackedMs: 7 * 60 * 60 * 1000,  // 7 hours
            focusTimeMs: 6 * 60 * 60 * 1000,     // 6 hours productive (86%)
            distractionMs: 30 * 60 * 1000,       // 30 min distraction
            distractionCount: 0,
            pauseCount: 0,
            idleCount: 0,
            longFocusBlockCount: 10);

        // Act
        var score = FocusScoreCalculator.Calculate(input);

        // Assert - With 86% productivity and max bonus
        score.Should().BeGreaterThan(85);
        score.Should().BeLessOrEqualTo(100);
    }

    // ============================================================================
    // Zero Focus Tests
    // ============================================================================

    [Fact]
    public void Calculate_WithZeroFocus_Returns0()
    {
        // Arrange - No productive time at all
        var input = FocusScoreInput.Create(
            totalTrackedMs: 8 * 60 * 60 * 1000,
            focusTimeMs: 0,
            distractionMs: 8 * 60 * 60 * 1000,
            distractionCount: 10,
            pauseCount: 5,
            idleCount: 3,
            longFocusBlockCount: 0);

        // Act
        var score = FocusScoreCalculator.Calculate(input);

        // Assert
        score.Should().Be(0);
    }

    [Fact]
    public void Calculate_WithEmptyInput_Returns0()
    {
        // Arrange - Empty input (no tracked time)
        var input = FocusScoreInput.Empty;

        // Act
        var score = FocusScoreCalculator.Calculate(input);

        // Assert
        score.Should().Be(0);
    }

    [Fact]
    public void Calculate_WithZeroTotalTracked_Returns0()
    {
        // Arrange
        var input = FocusScoreInput.Create(
            totalTrackedMs: 0,
            focusTimeMs: 0,
            distractionMs: 0,
            distractionCount: 0,
            pauseCount: 0,
            idleCount: 0,
            longFocusBlockCount: 0);

        // Act
        var score = FocusScoreCalculator.Calculate(input);

        // Assert
        score.Should().Be(0);
    }

    // ============================================================================
    // Penalty Tests
    // ============================================================================

    [Fact]
    public void Calculate_WithMaximumDistractionPenalty_CapsAt25()
    {
        // Arrange - Many distractions (should cap at 25 points penalty)
        var input = FocusScoreInput.Create(
            totalTrackedMs: 8 * 60 * 60 * 1000,
            focusTimeMs: 4 * 60 * 60 * 1000,  // 50% productive
            distractionMs: 2 * 60 * 60 * 1000,
            distractionCount: 20,             // 20 * 2.5 = 50, capped at 25
            pauseCount: 0,
            idleCount: 0,
            longFocusBlockCount: 0);

        // Act
        var score = FocusScoreCalculator.Calculate(input);

        // Assert - Base 50 - 25 penalty = 25
        score.Should().Be(25);
    }

    [Fact]
    public void Calculate_WithMaximumPausePenalty_CapsAt15()
    {
        // Arrange - Many pauses (should cap at 15 points penalty)
        var input = FocusScoreInput.Create(
            totalTrackedMs: 8 * 60 * 60 * 1000,
            focusTimeMs: 4 * 60 * 60 * 1000,  // 50% productive
            distractionMs: 0,
            distractionCount: 0,
            pauseCount: 20,                   // 20 * 1.5 = 30, capped at 15
            idleCount: 0,
            longFocusBlockCount: 0);

        // Act
        var score = FocusScoreCalculator.Calculate(input);

        // Assert - Base 50 - 15 penalty = 35
        score.Should().Be(35);
    }

    [Fact]
    public void Calculate_WithCombinedPenalties_ReducesScoreCorrectly()
    {
        // Arrange - Moderate distractions and pauses
        var input = FocusScoreInput.Create(
            totalTrackedMs: 8 * 60 * 60 * 1000,
            focusTimeMs: 6 * 60 * 60 * 1000,  // 75% productive
            distractionMs: 1 * 60 * 60 * 1000,
            distractionCount: 4,              // 4 * 2.5 = 10 penalty
            pauseCount: 4,                    // 4 * 1.5 = 6 penalty
            idleCount: 2,
            longFocusBlockCount: 0);

        // Act
        var score = FocusScoreCalculator.Calculate(input);

        // Assert - Base 75 - 10 - 6 = 59
        score.Should().Be(59);
    }

    // ============================================================================
    // Bonus Tests
    // ============================================================================

    [Fact]
    public void Calculate_WithLongFocusBlocks_AddsBonus()
    {
        // Arrange - 75% productive with focus blocks
        var input = FocusScoreInput.Create(
            totalTrackedMs: 8 * 60 * 60 * 1000,
            focusTimeMs: 6 * 60 * 60 * 1000,  // 75% productive
            distractionMs: 0,
            distractionCount: 0,
            pauseCount: 0,
            idleCount: 0,
            longFocusBlockCount: 3);          // 3 * 3 = 9 bonus

        // Act
        var score = FocusScoreCalculator.Calculate(input);

        // Assert - Base 75 + 9 = 84
        score.Should().Be(84);
    }

    [Fact]
    public void Calculate_WithMaximumFocusBlocks_CapsAt10()
    {
        // Arrange - Many focus blocks (should cap at 10 bonus)
        var input = FocusScoreInput.Create(
            totalTrackedMs: 8 * 60 * 60 * 1000,
            focusTimeMs: 6 * 60 * 60 * 1000,  // 75% productive
            distractionMs: 0,
            distractionCount: 0,
            pauseCount: 0,
            idleCount: 0,
            longFocusBlockCount: 10);         // 10 * 3 = 30, capped at 10

        // Act
        var score = FocusScoreCalculator.Calculate(input);

        // Assert - Base 75 + 10 = 85
        score.Should().Be(85);
    }

    // ============================================================================
    // Balanced Score Tests
    // ============================================================================

    [Fact]
    public void Calculate_WithRealisticWorkDay_ReturnsReasonableScore()
    {
        // Arrange - Realistic work day:
        // 7 hours tracked, 5 hours productive (71%)
        // 3 distractions, 2 pauses
        // 4 long focus blocks
        var input = FocusScoreInput.Create(
            totalTrackedMs: 7 * 60 * 60 * 1000,     // 7 hours
            focusTimeMs: 5 * 60 * 60 * 1000,        // 5 hours (71.4%)
            distractionMs: 1 * 60 * 60 * 1000,      // 1 hour
            distractionCount: 3,                    // 3 * 2.5 = 7.5 penalty
            pauseCount: 2,                          // 2 * 1.5 = 3 penalty
            idleCount: 1,
            longFocusBlockCount: 4);                // 4 * 3 = 12, capped at 10 bonus

        // Act
        var score = FocusScoreCalculator.Calculate(input);

        // Assert - Base 71.4 - 7.5 - 3 + 10 = 70.9 ≈ 71
        score.Should().BeInRange(65, 80);
    }

    [Fact]
    public void Calculate_WithLowProductivity_ReturnsLowScore()
    {
        // Arrange - Low productivity day
        var input = FocusScoreInput.Create(
            totalTrackedMs: 8 * 60 * 60 * 1000,
            focusTimeMs: 2 * 60 * 60 * 1000,   // 25% productive
            distractionMs: 4 * 60 * 60 * 1000,
            distractionCount: 8,
            pauseCount: 5,
            idleCount: 3,
            longFocusBlockCount: 0);

        // Act
        var score = FocusScoreCalculator.Calculate(input);

        // Assert - Should be low (25% - penalties)
        score.Should().BeLessThan(30);
    }

    // ============================================================================
    // Edge Cases
    // ============================================================================

    [Fact]
    public void Calculate_NeverReturnsNegative()
    {
        // Arrange - Extreme case that could go negative
        var input = FocusScoreInput.Create(
            totalTrackedMs: 8 * 60 * 60 * 1000,
            focusTimeMs: 30 * 60 * 1000,       // Only 30 min productive (6.25%)
            distractionMs: 4 * 60 * 60 * 1000,
            distractionCount: 50,              // Maximum distraction penalty
            pauseCount: 50,                    // Maximum pause penalty
            idleCount: 20,
            longFocusBlockCount: 0);

        // Act
        var score = FocusScoreCalculator.Calculate(input);

        // Assert - Should clamp to 0
        score.Should().Be(0);
    }

    [Fact]
    public void Calculate_NeverExceeds100()
    {
        // Arrange - Perfect metrics that could theoretically exceed 100
        var input = FocusScoreInput.Create(
            totalTrackedMs: 10 * 60 * 60 * 1000,
            focusTimeMs: 10 * 60 * 60 * 1000,  // 100% productive
            distractionMs: 0,
            distractionCount: 0,
            pauseCount: 0,
            idleCount: 0,
            longFocusBlockCount: 20);          // Would give 60 bonus without cap

        // Act
        var score = FocusScoreCalculator.Calculate(input);

        // Assert - Should clamp to 100
        score.Should().Be(100);
    }

    [Fact]
    public void Calculate_With50PercentProductivity_Returns50()
    {
        // Arrange - Exactly 50% with no modifiers
        var input = FocusScoreInput.Create(
            totalTrackedMs: 8 * 60 * 60 * 1000,
            focusTimeMs: 4 * 60 * 60 * 1000,   // 50%
            distractionMs: 4 * 60 * 60 * 1000,
            distractionCount: 0,
            pauseCount: 0,
            idleCount: 0,
            longFocusBlockCount: 0);

        // Act
        var score = FocusScoreCalculator.Calculate(input);

        // Assert
        score.Should().Be(50);
    }

    // ============================================================================
    // Breakdown Tests
    // ============================================================================

    [Fact]
    public void CalculateWithBreakdown_ReturnsDetailedBreakdown()
    {
        // Arrange
        var input = FocusScoreInput.Create(
            totalTrackedMs: 8 * 60 * 60 * 1000,
            focusTimeMs: 6 * 60 * 60 * 1000,
            distractionMs: 1 * 60 * 60 * 1000,
            distractionCount: 4,
            pauseCount: 3,
            idleCount: 1,
            longFocusBlockCount: 2);

        // Act
        var breakdown = FocusScoreCalculator.CalculateWithBreakdown(input);

        // Assert
        breakdown.FinalScore.Should().BeGreaterThan(0);
        breakdown.ProductivityRatio.Should().BeApproximately(0.75, 0.01);
        breakdown.BaseScore.Should().BeApproximately(75, 0.1);
        breakdown.DistractionPenalty.Should().Be(10); // 4 * 2.5
        breakdown.PausePenalty.Should().Be(4.5);      // 3 * 1.5
        breakdown.FocusBonus.Should().Be(6);          // 2 * 3
        breakdown.ProductivePercentage.Should().BeApproximately(75, 0.1);
    }

    [Fact]
    public void CalculateWithBreakdown_PerfectDay_ShowsAllZerosExceptBase()
    {
        // Arrange
        var input = FocusScoreInput.Create(
            totalTrackedMs: 8 * 60 * 60 * 1000,
            focusTimeMs: 8 * 60 * 60 * 1000,
            distractionMs: 0,
            distractionCount: 0,
            pauseCount: 0,
            idleCount: 0,
            longFocusBlockCount: 0);

        // Act
        var breakdown = FocusScoreCalculator.CalculateWithBreakdown(input);

        // Assert
        breakdown.FinalScore.Should().Be(100);
        breakdown.ProductivityRatio.Should().Be(1.0);
        breakdown.BaseScore.Should().Be(100);
        breakdown.DistractionPenalty.Should().Be(0);
        breakdown.PausePenalty.Should().Be(0);
        breakdown.FocusBonus.Should().Be(0);
    }
}
