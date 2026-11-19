using Cntryl.Portia;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers.Rules;

namespace Bdgrz.Compliance.Tests.Features.Providers.Tests;

public sealed class ProviderReviewTargetRulesTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void ShouldRequireNoReportDeclarationGivenQuestionnaireReview(bool declared, bool accepted)
    {
        // Arrange
        var content = AssuranceSamples.Review(Uuid.CreateVersion4()) with
        {
            EvidenceKind = "questionnaire",
            NoSoc2OrIso27001 = declared,
        };

        // Act
        var error = AssuranceRules.Validate(AssuranceRules.Normalize(content));

        // Assert
        Assert.Equal(accepted, error is null);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void ShouldRequireGapDetailsGivenAcceptableWithGaps(bool hasGap, bool accepted)
    {
        // Arrange
        var content = AssuranceSamples.Review(Uuid.CreateVersion4()) with
        {
            Conclusion = "acceptable_with_gaps",
            CoverageGapsFound = hasGap ? [Gap()] : [],
            CuecsAdopted = ["Review the provider access roster quarterly."],
        };

        // Act
        var error = AssuranceRules.Validate(AssuranceRules.Normalize(content));

        // Assert
        Assert.Equal(accepted, error is null);
    }

    [Fact]
    public void ShouldRejectDeclarationGivenSoc2Review()
    {
        // Arrange
        var content = AssuranceSamples.Review(Uuid.CreateVersion4()) with { NoSoc2OrIso27001 = true };

        // Act
        var error = AssuranceRules.Validate(AssuranceRules.Normalize(content));

        // Assert
        Assert.NotNull(error);
    }

    static ProviderCoverageGapContent Gap() => new(Uuid.CreateVersion4(), "Hosting", "Availability",
        new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), "assurance_report", Uuid.CreateVersion4(),
        1, "The January period was not covered.");
}
