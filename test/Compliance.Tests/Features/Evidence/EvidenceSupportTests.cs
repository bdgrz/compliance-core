using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Evidence;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Evidence;

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
        var request = new LinkEvidenceSupport(TenantId, ProgramId, ArtifactId, "control", ControlId,
            7, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), "Design review",
            "The evidence demonstrates the configured approval control.");
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
        var request = new LinkEvidenceSupport(TenantId, ProgramId, ArtifactId, "control", ControlId,
            7, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), "Design review", "Rationale");
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
        var request = new LinkEvidenceSupport(TenantId, ProgramId, ArtifactId, "control", ControlId,
            revision, new DateOnly(2026, 1, 1), new DateOnly(endYear, endMonth, endDay), "Review", "Rationale");
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
