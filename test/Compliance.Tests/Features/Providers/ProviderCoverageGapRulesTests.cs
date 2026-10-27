using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.Providers;
using Cntryl.Portia;
using static Bdgrz.Compliance.Tests.Features.Providers.AssuranceSamples;

namespace Bdgrz.Compliance.Tests.Features.Providers;

public sealed class ProviderCoverageGapRulesTests
{
    [Fact]
    public void ShouldRedactRestrictedProviderCoverageGapDetailsGivenReadOnlyViewer()
    {
        // Arrange
        var content = Valid() with { Description = "Confidential report detail." };
        var closure = new ProviderCoverageGapClosureView(
            new ProviderCoverageGapClosureContent("coverage_restored", "assurance_report",
                Uuid.CreateVersion4(), 3, "Confidential closure rationale."), Author, Now.AddDays(1))
        {
            Revision = 2,
        };
        var acceptance = new ProviderCoverageGapRiskAcceptanceView(Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), Now.AddMonths(2), Author, Now)
        {
            Revision = 2,
        };
        var gap = new ProviderCoverageGapView(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), 1, 2, content, "closed", Author, Now, closure, [acceptance]);

        // Act
        var redacted = ProviderCoverageGapRules.Redact(gap);

        // Assert
        Assert.True(redacted.Redacted);
        Assert.Equal("closed", redacted.Status);
        Assert.Equal("[redacted]", redacted.Content.Description);
        Assert.Equal("[redacted]", redacted.Closure!.Content.Rationale);
        Assert.Empty(redacted.RiskAcceptances);
    }

    [Fact]
    public void ShouldAcceptExactProviderCoverageGapGivenSupportedSource()
    {
        // Arrange
        var content = Valid();

        // Act
        var error = ProviderCoverageGapRules.InputError(content);

        // Assert
        Assert.Null(error);
    }

    [Theory]
    [InlineData("service")]
    [InlineData("assertion")]
    [InlineData("reversed_period")]
    [InlineData("source_kind")]
    [InlineData("source_id")]
    [InlineData("source_revision")]
    [InlineData("description")]
    public void ShouldRejectProviderCoverageGapGivenIncompleteOrInvalidFact(string fault)
    {
        // Arrange
        var content = fault switch
        {
            "service" => Valid() with { Service = " " },
            "assertion" => Valid() with { Assertion = " " },
            "reversed_period" => Valid() with { PeriodStart = new DateOnly(2026, 10, 2) },
            "source_kind" => Valid() with { SourceKind = "assurance_report_or_anything" },
            "source_id" => Valid() with { SourceId = Uuid.Empty },
            "source_revision" => Valid() with { SourceRevision = 0 },
            _ => Valid() with { Description = " " },
        };

        // Act
        var error = ProviderCoverageGapRules.InputError(content);

        // Assert
        Assert.NotNull(error);
    }

    static ProviderCoverageGapContent Valid() => new(
        Uuid.CreateVersion4(), "Payroll processing", "Availability assertion",
        new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30), "assurance_report",
        Uuid.CreateVersion4(), 2,
        "The SOC report does not cover the service for the full period.");
}
