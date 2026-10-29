using Cntryl.Portia;
using Cntryl.Portia.Testing;
using AuditOutcomeAggregate = Bdgrz.Compliance.Features.Examinations.Aggregates.AuditOutcome;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Examinations.Tests;

public sealed class AuditOutcomeTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid OutcomeId = Uuid.CreateVersion4();
    static readonly Uuid ExaminationId = Uuid.CreateVersion4();
    static readonly Uuid RecorderId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 17, 22, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRecordAuditorSuppliedOutcomeWithoutInferringProductReadinessGivenDeliveredPackage()
    {
        // Arrange
        var outcome = new AuditOutcomeAggregate(TenantId, ProgramId, OutcomeId);
        var content = Content();

        // Act
        var recorded = outcome.Record(content, RecorderId, "Audit manager", Now);

        // Assert
        Assert.Null(recorded);
        Assert.Equal("auditor", outcome.Current!.Content.SourceKind);
        Assert.Equal("qualified", outcome.Current!.Content.OutcomeCode);
        Assert.Equal("Auditor-supplied opinion summary", outcome.Current!.Content.OutcomeSummary);
        Assert.Equal(content.ReportArtifactSha256, outcome.Current!.Content.ReportArtifactSha256);
        Assert.Equal(content.PackageManifestSha256, outcome.Current!.Content.PackageManifestSha256);
    }

    [Fact]
    public void ShouldRejectProductCalculatedOpinionGivenOutcomeContent()
    {
        // Arrange
        var outcome = new AuditOutcomeAggregate(TenantId, ProgramId, OutcomeId);

        // Act
        var failure = outcome.Record(Content() with { SourceKind = "product_calculated" },
            RecorderId, "Audit manager", Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent,
            Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(0, outcome.Revision);
    }

    [Fact]
    public void ShouldTreatExactOutcomeRetryAsIdempotentGivenLaterRecordingTime()
    {
        // Arrange
        var outcome = new AuditOutcomeAggregate(TenantId, ProgramId, OutcomeId);
        var content = Content();
        Assert.Null(outcome.Record(content, RecorderId, "Audit manager", Now));

        // Act
        var retried = outcome.Record(content, RecorderId, "Updated display name",
            Now.AddMinutes(5));

        // Assert
        Assert.Null(retried);
        Assert.Equal(1, outcome.Revision);
        Assert.Equal(Now, outcome.Current!.RecordedAt);
        Assert.Equal("Audit manager", outcome.Current.RecordedByDisplay);
    }

    [Fact]
    public void ShouldRejectChangedOutcomeRetryGivenRecordedOutcomeIdentity()
    {
        // Arrange
        var outcome = new AuditOutcomeAggregate(TenantId, ProgramId, OutcomeId);
        var content = Content();
        Assert.Null(outcome.Record(content, RecorderId, "Audit manager", Now));

        // Act
        var changed = outcome.Record(content with { OutcomeSummary = "Changed auditor opinion." },
            RecorderId, "Audit manager", Now.AddMinutes(5));

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict,
            Assert.IsType<CommandFailure>(changed).Code);
        Assert.Equal(1, outcome.Revision);
        Assert.Equal(content.OutcomeSummary, outcome.Current!.Content.OutcomeSummary);
    }

    [Fact]
    public void ShouldReplayAuditorOutcomeAndExactReportBindingGivenRecordedOutcome()
    {
        // Arrange
        var outcome = new AuditOutcomeAggregate(TenantId, ProgramId, OutcomeId);
        Assert.Null(outcome.Record(Content(), RecorderId, "Audit manager", Now));
        var events = new AggregateScenario<AuditOutcomeAggregate>(outcome).PendingEvents.ToArray();

        // Act
        var replay = new AggregateScenario<AuditOutcomeAggregate>(
            new AuditOutcomeAggregate(TenantId, ProgramId, OutcomeId)).Given(events).Aggregate;

        // Assert
        Assert.Equal(outcome.Current!.ContentSha256, replay.Current!.ContentSha256);
        Assert.Equal("auditor", replay.Current.Content.SourceKind);
        Assert.Equal("report-ref-2026-09", replay.Current.Content.ReportReference);
    }

    static AuditOutcomeContent Content() => new(ExaminationId, "type_i",
        Uuid.CreateVersion4(), new string('a', 64), Uuid.CreateVersion4(), 1,
        Uuid.CreateVersion4(), new string('b', 64), "auditor", "qualified",
        "Auditor-supplied opinion summary", "report-ref-2026-09", Uuid.CreateVersion4(),
        new string('c', 64), new DateOnly(2026, 9, 30));
}
