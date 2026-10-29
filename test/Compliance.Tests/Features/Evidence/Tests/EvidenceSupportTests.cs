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
using Bdgrz.Compliance.Tests.Testing;
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

    static readonly string Hash = new('a', 64);

    static LinkEvidenceSupport Request(long expectedRevision = 0) => new(Uuid.CreateVersion4(), TenantId, ProgramId,
        ArtifactId, "control", ControlId, 7, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), "Design review",
        "Rationale", expectedRevision);

    static ActorReference Lead() => ActorReference.ForMember(MemberId, "Evidence lead");

    [Fact]
    public void ShouldRejectWhitespaceActorDisplayWithoutEventGivenLink()
    {
        // Arrange
        var request = Request();
        var support = new EvidenceSupport(TenantId, ProgramId, EvidenceSupport.IdFor(request));

        // Act
        var failure = support.Link(request, 2, Hash, ActorReference.ForMember(MemberId, "   "), Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Empty(new AggregateScenario<EvidenceSupport>(support).PendingEvents);
        Assert.Equal(0, support.Revision);
    }

    [Fact]
    public void ShouldRejectNonMemberActorWithoutEventGivenLink()
    {
        // Arrange
        var request = Request();
        var support = new EvidenceSupport(TenantId, ProgramId, EvidenceSupport.IdFor(request));

        // Act
        var failure = support.Link(request, 2, Hash, new ActorReference("service", MemberId.ToString(), "Collector"),
            Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Empty(new AggregateScenario<EvidenceSupport>(support).PendingEvents);
    }

    [Theory]
    [InlineData("not-a-type", "Rationale", 2, "Review")]
    [InlineData("control", "   ", 2, "Review")]
    [InlineData("control", "Rationale", 0, "Review")]
    [InlineData("control", "Rationale", 2, "")]
    public void ShouldRejectInvalidSourceOrTextWithoutEventGivenLink(string recordType, string rationale,
        long artifactRevision, string purpose)
    {
        // Arrange
        var request = Request() with
        {
            SupportedRecordType = recordType,
            SufficiencyRationale = rationale,
            Purpose = purpose,
        };
        var support = new EvidenceSupport(TenantId, ProgramId, EvidenceSupport.IdFor(request));

        // Act
        var failure = support.Link(request, artifactRevision, Hash, Lead(), Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Empty(new AggregateScenario<EvidenceSupport>(support).PendingEvents);
    }

    [Fact]
    public void ShouldRejectOversizeRationaleOrActorDisplayWithoutEventGivenLink()
    {
        // Arrange
        var request = Request();
        var support = new EvidenceSupport(TenantId, ProgramId, EvidenceSupport.IdFor(request));

        // Act
        var longRationale = support.Link(request with { SufficiencyRationale = new string('r', 2001) }, 2, Hash,
            Lead(), Now);
        var longDisplay = support.Link(request, 2, Hash, ActorReference.ForMember(MemberId, new string('d', 201)), Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(longRationale).Code);
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(longDisplay).Code);
        Assert.Empty(new AggregateScenario<EvidenceSupport>(support).PendingEvents);
    }

    [Fact]
    public void ShouldRejectMalformedArtifactHashWithoutEventGivenLink()
    {
        // Arrange
        var request = Request();
        var support = new EvidenceSupport(TenantId, ProgramId, EvidenceSupport.IdFor(request));

        // Act
        var failure = support.Link(request, 2, new string('A', 64), Lead(), Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Empty(new AggregateScenario<EvidenceSupport>(support).PendingEvents);
    }

    [Fact]
    public void ShouldRejectOtherTenantOrIdentityWithoutEventGivenLink()
    {
        // Arrange
        var request = Request();
        var otherTenant = new EvidenceSupport(TenantId, ProgramId, EvidenceSupport.IdFor(request));
        var otherId = new EvidenceSupport(TenantId, ProgramId, Uuid.CreateVersion4());

        // Act
        var wrongTenant = otherTenant.Link(request with { TenantId = Uuid.CreateVersion4() }, 2, Hash, Lead(), Now);
        var wrongId = otherId.Link(request, 2, Hash, Lead(), Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(wrongTenant).Code);
        Assert.Equal(CommandFailureCode.InvalidContent, Assert.IsType<CommandFailure>(wrongId).Code);
        Assert.Empty(new AggregateScenario<EvidenceSupport>(otherTenant).PendingEvents);
        Assert.Empty(new AggregateScenario<EvidenceSupport>(otherId).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseStaleRevisionWithoutEventGivenNewSupport()
    {
        // Arrange
        var request = Request(expectedRevision: 3);
        var support = new EvidenceSupport(TenantId, ProgramId, EvidenceSupport.IdFor(request));

        // Act
        var failure = support.Link(request, 2, Hash, Lead(), Now);

        // Assert
        Assert.Equal(CommandFailureCode.VersionConflict, Assert.IsType<CommandFailure>(failure).Code);
        Assert.Empty(new AggregateScenario<EvidenceSupport>(support).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseChangedActorOrExpectedRevisionGivenCommittedSupport()
    {
        // Arrange
        var request = Request();
        var support = new EvidenceSupport(TenantId, ProgramId, EvidenceSupport.IdFor(request));
        Assert.Null(support.Link(request, 2, Hash, Lead(), Now));

        // Act
        var otherActor = support.Link(request, 2, Hash,
            ActorReference.ForMember(Uuid.CreateVersion4(), "Another lead"), Now);
        var otherRevision = support.Link(request with { ExpectedRevision = 1 }, 2, Hash, Lead(), Now);

        // Assert
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(otherActor).Code);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(otherRevision).Code);
        Assert.Single(new AggregateScenario<EvidenceSupport>(support).PendingEvents);
    }

    [Fact]
    public void ShouldReturnOriginalViewGivenRetryWithLaterArtifactFacts()
    {
        // Arrange
        var request = Request();
        var support = new EvidenceSupport(TenantId, ProgramId, EvidenceSupport.IdFor(request));
        Assert.Null(support.Link(request, 2, Hash, Lead(), Now));

        // Act
        var retry = support.Link(request, 9, new string('b', 64), Lead(), Now.AddDays(3));
        var replay = support.Replay(request, Lead());

        // Assert
        Assert.Null(retry);
        Assert.True(replay!.Value.IsSuccess);
        Assert.Equal(2, support.View()!.ArtifactRevision);
        Assert.Equal(Hash, support.View()!.ArtifactContentSha256);
        Assert.Equal(Now, support.View()!.LinkedAt);
        Assert.Single(new AggregateScenario<EvidenceSupport>(support).PendingEvents);
    }

    [Fact]
    public void ShouldRestoreSupportAndRefuseChangedIntentGivenValidHistory()
    {
        // Arrange
        var request = Request();
        var id = EvidenceSupport.IdFor(request);
        var original = new EvidenceSupport(TenantId, ProgramId, id);
        Assert.Null(original.Link(request, 2, Hash, Lead(), Now));
        var facts = NativeFacts.Raised(original);

        // Act
        var restored = NativeFacts.Replay(new EvidenceSupport(TenantId, ProgramId, id), facts);
        var revision = restored.Revision;
        var view = restored.View();
        var retry = restored.Link(request, 5, new string('c', 64), Lead(), Now.AddDays(1));
        var changed = restored.Link(request with { Purpose = "Other" }, 5, Hash, Lead(), Now.AddDays(1));

        // Assert
        Assert.Equal(1, revision);
        Assert.Equal(ArtifactId, view!.ArtifactId);
        Assert.Equal(2, view.ArtifactRevision);
        Assert.Equal(MemberId, view.ActorMemberId);
        Assert.Null(retry);
        Assert.Equal(CommandFailureCode.StateConflict, Assert.IsType<CommandFailure>(changed).Code);
        Assert.Empty(new AggregateScenario<EvidenceSupport>(restored).PendingEvents);
    }
}
