using Bdgrz.Compliance.Features.Examinations;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using ExaminationSignOffAggregate = Bdgrz.Compliance.Features.Examinations.ExaminationSignOff;

namespace Bdgrz.Compliance.Tests.Features.Examinations;

public sealed class ExaminationSignOffTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid SignOffId = Uuid.CreateVersion4();
    static readonly Uuid ExaminationId = Uuid.CreateVersion4();
    static readonly Uuid CEOId = Uuid.CreateVersion4();
    static readonly Uuid CISOId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 17, 23, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldBindManagementApprovalToExactInputsAndAcknowledgeEveryOpenItemGivenOutcome()
    {
        // Arrange
        var signOff = new ExaminationSignOffAggregate(TenantId, ProgramId, SignOffId);
        var content = Content();

        // Act
        var missingAcknowledgement = signOff.SignOff(content with
        {
            AcknowledgedMaterialItemIds = [],
        }, CEOId, "CEO", "Chief Executive", Now);
        var signed = signOff.SignOff(content, CEOId, "CEO", "Chief Executive", Now);

        // Assert
        Assert.Equal(CommandFailureCode.InvalidContent,
            Assert.IsType<CommandFailure>(missingAcknowledgement).Code);
        Assert.Null(signed);
        Assert.Equal(content.PackageManifestSha256, signOff.Current!.Content.PackageManifestSha256);
        Assert.Equal(content.AssertionContentSha256, signOff.Current!.Content.AssertionContentSha256);
        Assert.Equal("approved", signOff.Current!.Content.Decision);
        Assert.Equal(CEOId, signOff.Current!.Content.Approvers.Single().MemberId);
        Assert.Equal(content.UnresolvedMaterialItemIds.Order(),
            signOff.Current!.Content.AcknowledgedMaterialItemIds.Order());
    }

    [Fact]
    public void ShouldRequireAuthorizedManagementSignerGivenCompleteExternalOutcome()
    {
        // Arrange
        var signOff = new ExaminationSignOffAggregate(TenantId, ProgramId, SignOffId);

        // Act
        var failure = signOff.SignOff(Content(), Uuid.CreateVersion4(), "Contributor", "Contributor", Now);

        // Assert
        Assert.Equal(CommandFailureCode.ActorProhibited,
            Assert.IsType<CommandFailure>(failure).Code);
        Assert.Equal(0, signOff.Revision);
    }

    [Fact]
    public void ShouldRetainAttributedFinalCommitmentsAndDueDatesGivenTypeIISignOff()
    {
        // Arrange
        var signOff = new ExaminationSignOffAggregate(TenantId, ProgramId, SignOffId);
        var commitment = new ExaminationCommitment(Uuid.CreateVersion4(),
            "Complete the access review evidence remediation.", CISOId,
            new DateOnly(2027, 2, 15), "exception", Uuid.CreateVersion4(), 3,
            new string('f', 64));
        var content = Content() with { ScopeKind = "type_ii", Commitments = [commitment] };

        // Act
        var signed = signOff.SignOff(content, CEOId, "CEO", "Chief Executive", Now);

        // Assert
        Assert.Null(signed);
        Assert.Equal("type_ii", signOff.Current!.Content.ScopeKind);
        Assert.Equal(commitment, signOff.Current.Content.Commitments.Single());
    }

    [Fact]
    public void ShouldReplayImmutableManagementDecisionGivenSignOffEvent()
    {
        // Arrange
        var signOff = new ExaminationSignOffAggregate(TenantId, ProgramId, SignOffId);
        Assert.Null(signOff.SignOff(Content(), CEOId, "CEO", "Chief Executive", Now));
        var events = new AggregateScenario<ExaminationSignOffAggregate>(signOff).PendingEvents.ToArray();

        // Act
        var replay = new AggregateScenario<ExaminationSignOffAggregate>(
            new ExaminationSignOffAggregate(TenantId, ProgramId, SignOffId)).Given(events).Aggregate;

        // Assert
        Assert.Equal("approved", replay.Current!.Status);
        Assert.Equal(signOff.Current!.Content.PackageManifestSha256,
            replay.Current.Content.PackageManifestSha256);
        Assert.Equal(signOff.Current.Content.KnownExceptions.ToArray(),
            replay.Current.Content.KnownExceptions.ToArray());
        Assert.Equal(signOff.Current.Content.UnresolvedMaterialItemIds.ToArray(),
            replay.Current.Content.UnresolvedMaterialItemIds.ToArray());
        Assert.Equal(CEOId, replay.Current.SignedOffByMemberId);
    }

    static ExaminationSignOffContent Content()
    {
        var unresolvedItemId = Uuid.CreateVersion4();
        return new ExaminationSignOffContent(ExaminationId, "type_i",
            Uuid.CreateVersion4(), new string('a', 64), Uuid.CreateVersion4(), 1,
            Uuid.CreateVersion4(), new string('b', 64), Uuid.CreateVersion4(), 1,
            new string('c', 64), Uuid.CreateVersion4(), new string('d', 64), Uuid.CreateVersion4(),
            new string('e', 64), [new("finding", Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1,
                new string('f', 64))], [unresolvedItemId],
            [unresolvedItemId], "approved",
            "Reviewed the external report, exact package, assertion, and open item.",
            [new(CEOId, "CEO")]);
    }
}
