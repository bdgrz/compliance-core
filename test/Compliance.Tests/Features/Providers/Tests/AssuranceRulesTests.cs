using Cntryl.Portia;
using static Bdgrz.Compliance.Tests.Features.Providers.Tests.AssuranceSamples;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.Snapshots.Projections;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.Providers.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Providers.Tests;

public sealed class AssuranceRulesTests
{
    [Theory]
    [InlineData("kind", "soc1")]
    [InlineData("issuer", "")]
    [InlineData("scope", "  ")]
    [InlineData("opinion", "clean")]
    [InlineData("opinion_missing", "")]
    [InlineData("opinion_source_missing", "")]
    [InlineData("type1_period", "")]
    [InlineData("type2_period_missing", "")]
    [InlineData("period_reversed", "")]
    [InlineData("iso_opinion", "")]
    [InlineData("bridge_overlaps_period", "")]
    [InlineData("bridge_reversed", "")]
    [InlineData("citation_unclassified", "")]
    [InlineData("citation_unknown_class", "secret")]
    [InlineData("empty_exception", "")]
    [InlineData("too_many_gaps", "")]
    public void ShouldRejectReportGivenInvalidFact(string fault, string value)
    {
        // Arrange
        var good = Report();
        var content = fault switch
        {
            "kind" => good with { ReportKind = value },
            "issuer" => good with { Issuer = value },
            "scope" => good with { Scope = value },
            "opinion" => good with { Opinion = value },
            "opinion_missing" => good with { Opinion = null, OpinionSource = null },
            "opinion_source_missing" => good with { OpinionSource = null },
            "type1_period" => good with { ReportKind = "soc2_type1" },
            "type2_period_missing" => good with { PeriodStart = null },
            "period_reversed" => good with { PeriodStart = new DateOnly(2026, 7, 1) },
            "iso_opinion" => good with { ReportKind = "iso27001", PeriodStart = null },
            "bridge_overlaps_period" => good with { BridgeLetter = new AssuranceBridgeLetter(new DateOnly(2026, 8, 1), new DateOnly(2026, 6, 30), new DateOnly(2026, 12, 31)) },
            "bridge_reversed" => good with { BridgeLetter = new AssuranceBridgeLetter(new DateOnly(2026, 8, 1), new DateOnly(2026, 12, 31), new DateOnly(2026, 7, 31)) },
            "citation_unclassified" => good with { Citation = Citation(value) },
            "citation_unknown_class" => good with { Citation = Citation(value) },
            "empty_exception" => good with { Exceptions = [new AssuranceExceptionNote(null, " ")] },
            _ => good with { CoverageGaps = Enumerable.Repeat("gap", 51).ToArray() },
        };

        // Act
        var error = AssuranceRules.InputError(content) ?? AssuranceRules.Validate(AssuranceRules.Normalize(content));

        // Assert
        Assert.NotNull(error);
    }

    [Fact]
    public void ShouldAcceptRestrictedCitationAndNoOpinionGivenIsoCertificate()
    {
        // Arrange
        var iso = new AssuranceReportContent("iso27001", "Certification Body", "ISMS for hosting", new DateOnly(2026, 3, 1),
            Citation: Citation("restricted"));

        // Act
        var error = AssuranceRules.Validate(AssuranceRules.Normalize(iso));

        // Assert
        Assert.Null(error);
    }

    [Fact]
    public void ShouldAcceptLargeContentGivenBoundedFieldsAtTheirLimits()
    {
        // Arrange
        var exceptions = Enumerable.Range(0, 50).Select(_ => new AssuranceExceptionNote(new string('r', 200), new string('d', 2000))).ToArray();
        var content = Report() with
        {
            Exceptions = exceptions,
            CoverageGaps = Enumerable.Repeat(new string('g', 2000), 50).ToArray(),
            ComplementaryControls = Enumerable.Repeat(new string('c', 2000), 50).ToArray(),
        };

        // Act
        var error = AssuranceRules.InputError(content) ?? AssuranceRules.Validate(AssuranceRules.Normalize(content));

        // Assert
        Assert.Null(error);
    }

    [Fact]
    public void ShouldReturnInputErrorGivenNullNestedEntries()
    {
        // Arrange
        var reportContent = Report() with { Exceptions = [null!] };
        var reviewContent = Review() with { Exceptions = [null!] };

        // Act
        var report = AssuranceRules.InputError(reportContent);
        var review = AssuranceRules.InputError(reviewContent);

        // Assert
        Assert.NotNull(report);
        Assert.NotNull(review);
    }

    [Theory]
    [InlineData("next_not_after_review")]
    [InlineData("no_evidence")]
    [InlineData("exceptions_with_acceptable")]
    [InlineData("missing_exceptions")]
    [InlineData("unknown_conclusion")]
    [InlineData("kind")]
    public void ShouldRejectReviewGivenUnsupportedFact(string fault)
    {
        // Arrange
        var good = Review(Uuid.CreateVersion4());
        var content = fault switch
        {
            "next_not_after_review" => good with { NextReviewDue = good.ReviewedAt },
            "no_evidence" => good with { AssuranceReportId = null },
            "exceptions_with_acceptable" => good with { Exceptions = ["Unexpected"] },
            "missing_exceptions" => good with { Conclusion = "acceptable_with_exceptions" },
            "kind" => good with { EvidenceKind = "soc1" },
            _ => good with { Conclusion = "approved" },
        };

        // Act
        var error = AssuranceRules.Validate(AssuranceRules.Normalize(content));

        // Assert
        Assert.NotNull(error);
    }

    [Fact]
    public void ShouldRedactRestrictedCitationGivenReportWithoutAuthoringAuthority()
    {
        // Arrange
        var view = ReportView(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1,
            Report("restricted") with { Exceptions = [new AssuranceExceptionNote("CC6.1", "Exception text")] });

        // Act
        var redacted = AssuranceRules.Redact(view);

        // Assert
        Assert.True(redacted.Redacted);
        Assert.Null(redacted.Content.Citation);
        Assert.Empty(redacted.Content.Exceptions!);
        Assert.Equal(1, redacted.ExceptionCount);
    }
}
