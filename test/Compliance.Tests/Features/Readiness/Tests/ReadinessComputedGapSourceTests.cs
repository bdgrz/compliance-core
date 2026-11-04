using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Readers;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.Readiness.Tests;

public sealed class ReadinessComputedGapSourceTests
{
    [Fact]
    public void ShouldReplaceOnlyNamedUnknownFamilyGivenAuthoritativeContribution()
    {
        // Arrange
        var program = Uuid.CreateVersion4();
        var original = new ReadinessEvaluation([new("workforce", "not_assessed", 0, "Unknown workforce"),
            new("evidence", "not_assessed", 0, "Unknown evidence")], [],
            [Unknown(program, "workforce"), Unknown(program, "evidence")], new string('a', 64));
        var contribution = new ReadinessComputedGapSet("workforce", [new("workforce", "assessed", 0, "No workforce gaps")], [], []);

        // Act
        var merged = ReadinessComputedGapComposition.Compose(program, original, [contribution]);

        // Assert
        Assert.Equal("incomplete", merged.Completeness.Verdict);
        Assert.Equal("evidence", Assert.Single(merged.Completeness.IncompleteInputs).Family);
        Assert.DoesNotContain(merged.Gaps, gap => gap.Subject == "workforce");
        Assert.Contains(merged.Gaps, gap => gap.Subject == "evidence");
        Assert.NotEqual(original.InputFingerprint, merged.InputFingerprint);
    }

    [Fact]
    public void ShouldRemainIncompleteGivenContributorReportsDraftInput()
    {
        // Arrange
        var contribution = new ReadinessComputedGapSet("evidence", [new("evidence", "draft", 1, "Evidence remains a draft")], [], []);

        // Act
        var merged = ReadinessComputedGapComposition.Compose(Uuid.CreateVersion4(), new ReadinessEvaluation([], [], [], new string('a', 64)), [contribution]);

        // Assert
        Assert.Equal("incomplete", merged.Completeness.Verdict);
        Assert.Equal("draft", Assert.Single(merged.Completeness.IncompleteInputs).Status);
    }

    [Theory]
    [InlineData(null, null, "unknown")]
    [InlineData(1, "draft", "draft")]
    [InlineData(2, "active", "assessed")]
    public void ShouldClassifyTechnologySourceGivenExactRevisionAndLifecycle(long? revision, string? lifecycle, string expectedStatus)
    {
        // Arrange
        var program = Uuid.CreateVersion4();
        ReadinessTechnologyInventoryInput[] records = [new("system_instance", Uuid.CreateVersion4(), revision, lifecycle, DateTimeOffset.UtcNow)];

        // Act
        var result = TechnologyInventoryReadinessGapSource.Evaluate(program, records);

        // Assert
        Assert.Equal(expectedStatus, Assert.Single(result.Inputs).Status);
        Assert.Equal("technology_inventory", result.Family);
    }

    static ReadinessGapView Unknown(Uuid program, string family) => new(ReadinessRules.GapIdFor(program,
        ReadinessRules.SourceFamilyAssessed, family), "input_not_assessed", family,
        ReadinessRules.SourceFamilyAssessed, "Unknown input", []);
}
