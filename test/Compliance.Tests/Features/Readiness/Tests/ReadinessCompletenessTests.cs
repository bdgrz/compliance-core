using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Readiness.Tests;

public sealed class ReadinessCompletenessTests
{
    [Theory]
    [InlineData("unknown")]
    [InlineData("draft")]
    [InlineData("not_assessed")]
    [InlineData("truncated")]
    public void ShouldReportIncompleteGivenUnknownOrDraftInput(string status)
    {
        // Arrange
        ReadinessInputView[] inputs = [new("controls", "assessed", 1, "Known"), new("evidence", status, 0, "Unavailable")];

        // Act
        var result = ReadinessCompletenessRules.Evaluate(inputs, []);

        // Assert
        Assert.False(result.IsComplete);
        Assert.Equal("incomplete", result.Verdict);
        var missing = Assert.Single(result.IncompleteInputs);
        Assert.Equal("evidence", missing.Family);
        Assert.Equal(status, missing.Status);
    }

    [Fact]
    public void ShouldPreferIncompleteGivenKnownGapAndUnknownInput()
    {
        // Arrange
        ReadinessInputView[] inputs = [new("evidence", "unknown", 0, "Missing source")];
        ReadinessGapView[] gaps = [Gap()];

        // Act
        var result = ReadinessCompletenessRules.Evaluate(inputs, gaps);

        // Assert
        Assert.Equal("incomplete", result.Verdict);
    }

    [Theory]
    [InlineData(false, "ready")]
    [InlineData(true, "not_ready")]
    public void ShouldDistinguishCompleteVerdictsGivenKnownInputs(bool hasGap, string expected)
    {
        // Arrange
        ReadinessInputView[] inputs = [new("controls", "assessed", 1, "Known")];
        ReadinessGapView[] gaps = hasGap ? [Gap()] : [];

        // Act
        var result = ReadinessCompletenessRules.Evaluate(inputs, gaps);

        // Assert
        Assert.True(result.IsComplete);
        Assert.Equal(expected, result.Verdict);
        Assert.Empty(result.IncompleteInputs);
    }

    [Fact]
    public void ShouldPreserveEveryExplanationGivenMultipleIncompleteInputs()
    {
        // Arrange
        ReadinessInputView[] inputs = [new("evidence", "draft", 1, "Draft evidence"), new("workforce", "unknown", 0, "Unknown roster")];

        // Act
        var result = ReadinessCompletenessRules.Evaluate(inputs, []);

        // Assert
        Assert.Equal(new[] { "evidence", "workforce" }, result.IncompleteInputs.Select(input => input.Family));
        Assert.Equal(new[] { "Draft evidence", "Unknown roster" }, result.IncompleteInputs.Select(input => input.Explanation));
    }

    static ReadinessGapView Gap() => new(Uuid.CreateVersion4(), "unmet", "control", "rule", "Known unmet rule", []);
}
