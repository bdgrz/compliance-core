using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Versioning.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
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
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Evidence.Events;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Evidence.Tests;

public sealed class EvidenceSupportTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid ArtifactId = Uuid.CreateVersion4();
    static readonly Uuid ControlId = Uuid.CreateVersion4();
    static readonly Uuid MemberId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 17, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRecordExactAttributableSupportRelationshipGivenValidLink()
    {
        // Arrange
        var request = new LinkEvidenceSupport(Uuid.CreateVersion4(), TenantId, ProgramId, ArtifactId, "control", ControlId,
            7, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), "Design review",
            "The evidence demonstrates the configured approval control.", 0);
        var supportId = EvidenceSupport.IdFor(request);
        var support = new EvidenceSupport(TenantId, ProgramId, supportId);

        // Act
        var failure = support.Link(request, 2, new string('a', 64),
            ActorReference.ForMember(MemberId, "Evidence lead"), Now);

        // Assert
        Assert.Null(failure);
        Assert.Equal(1, support.Revision);
        Assert.Equal(ArtifactId, support.Current!.ArtifactId);
        Assert.Equal(2, support.Current.ArtifactRevision);
        Assert.Equal(new string('a', 64), support.Current.ArtifactContentSha256);
        Assert.Equal("control", support.Current.SupportedRecordType);
        Assert.Equal(ControlId, support.Current.SupportedRecordId);
        Assert.Equal(7, support.Current.SupportedRecordRevision);
        Assert.Equal(new DateOnly(2026, 1, 1), support.Current.PeriodStart);
        Assert.Equal(new DateOnly(2026, 12, 31), support.Current.PeriodEnd);
        Assert.Equal("Design review", support.Current.Purpose);
        Assert.Equal("The evidence demonstrates the configured approval control.",
            support.Current.SufficiencyRationale);
        Assert.Equal(MemberId, support.Current.ActorMemberId);
        var ev = Assert.IsType<EvidenceSupportLinked>(
            Assert.Single(new AggregateScenario<EvidenceSupport>(support).PendingEvents));
        Assert.Equal(supportId, ev.SubjectId);
        Assert.Equal(ProgramId, ev.ProgramId);
        Assert.Equal(ArtifactId, ev.ArtifactId);
        Assert.Equal(2, ev.ArtifactRevision);
        Assert.Equal("control", ev.SupportedRecordType);
        Assert.Equal(ControlId, ev.SupportedRecordId);
        Assert.Equal(7, ev.SupportedRecordRevision);
    }

    [Fact]
    public void ShouldAcceptExactReplayAndRejectChangedRelationshipGivenStableSupportIdentity()
    {
        // Arrange
        var request = new LinkEvidenceSupport(Uuid.CreateVersion4(), TenantId, ProgramId, ArtifactId, "control", ControlId,
            7, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), "Design review", "Rationale", 0);
        var support = new EvidenceSupport(TenantId, ProgramId, EvidenceSupport.IdFor(request));
        Assert.Null(support.Link(request, 2, new string('a', 64),
            ActorReference.ForMember(MemberId, "Evidence lead"), Now));

        // Act
        var retry = support.Link(request, 2, new string('a', 64),
            ActorReference.ForMember(MemberId, "Evidence lead"), Now);
        var changed = support.Link(request with { SufficiencyRationale = "Different rationale" },
            2, new string('a', 64), ActorReference.ForMember(MemberId, "Evidence lead"), Now.AddMinutes(1));

        // Assert
        Assert.Null(retry);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(changed).Code);
        Assert.Equal(1, support.Revision);
        Assert.Single(new AggregateScenario<EvidenceSupport>(support).PendingEvents);
    }

    [Theory]
    [InlineData(0, 2026, 12, 31)]
    [InlineData(7, 2025, 12, 31)]
    public void ShouldRejectInvalidRevisionOrPeriodWithoutEventGivenLink(int revision,
        int endYear, int endMonth, int endDay)
    {
        // Arrange
        var request = new LinkEvidenceSupport(Uuid.CreateVersion4(), TenantId, ProgramId, ArtifactId, "control", ControlId,
            revision, new DateOnly(2026, 1, 1), new DateOnly(endYear, endMonth, endDay), "Review", "Rationale", 0);
        var support = new EvidenceSupport(TenantId, ProgramId, EvidenceSupport.IdFor(request));

        // Act
        var failure = support.Link(request, 2, new string('a', 64),
            ActorReference.ForMember(MemberId, "Evidence lead"), Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Empty(new AggregateScenario<EvidenceSupport>(support).PendingEvents);
        Assert.Equal(0, support.Revision);
    }
}
