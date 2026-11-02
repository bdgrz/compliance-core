using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
namespace Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;

public sealed class PolicyDistributionCampaignLifecycleTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly Uuid Campaign = Uuid.CreateVersion4();
    static readonly Uuid Program = Uuid.CreateVersion4();
    static readonly CampaignSubject PolicySubject = new("policy", Uuid.CreateVersion4(), "POL-AC",
        "Access Control Policy", 2, "sha-v2");
    static readonly DateOnly Today = DateOnly.FromDateTime(NativeFacts.Now.UtcDateTime);

    [Fact]
    public void ShouldDeriveStreamFromTenantAndCampaignGivenAnySelectedLogicalId()
    {
        // Arrange
        var expected = new EventStreamAddress(Tenant.ToString(), PolicyDistributionCampaign.Area, Campaign.ToString());

        // Act
        var plain = new PolicyDistributionCampaign(Tenant, Campaign);
        var selected = new PolicyDistributionCampaign(Tenant, Campaign, Uuid.CreateVersion4());

        // Assert
        Assert.Equal(expected, plain.Stream);
        Assert.Equal(expected, selected.Stream);
        Assert.Equal(Campaign, selected.Id);
    }

    [Fact]
    public void ShouldLaunchAndFreezeAtRevisionOneGivenUnlaunchedPolicyCampaign()
    {
        // Arrange
        var owner = new PolicyDistributionCampaign(Tenant, Campaign);
        var member = Uuid.CreateVersion4();

        // Act
        var result = Launch(owner, PolicySubject, member);

        // Assert
        Assert.True(result.IsSuccess);
        var pending = new AggregateScenario<PolicyDistributionCampaign>(owner).PendingEvents;
        Assert.Equal(2, pending.Count);
        var launched = Assert.IsType<PolicyCampaignLaunched>(pending[0]);
        Assert.IsType<PolicyCampaignAudienceFrozen>(pending[1]);
        Assert.Equal(PolicyDistributionCampaign.DefaultAcknowledgementText, launched.AcknowledgementText);
        Assert.Equal(Campaign, launched.CampaignId);
        Assert.Equal(member, launched.ActorMemberId);
        Assert.True(owner.IsLaunched);
        Assert.False(owner.IsSealed);
        Assert.Equal(1, owner.Revision);
        Assert.Equal(1, owner.Generation);
    }

    [Fact]
    public void ShouldOmitAcknowledgementTextGivenTrainingCampaign()
    {
        // Arrange
        var owner = new PolicyDistributionCampaign(Tenant, Campaign);
        var training = new CampaignSubject("training", Uuid.CreateVersion4(), "SAT", "Security awareness", 1, "sha-course");

        // Act
        var result = Launch(owner, training, Uuid.CreateVersion4());

        // Assert
        Assert.True(result.IsSuccess);
        var launched = Assert.IsType<PolicyCampaignLaunched>(
            new AggregateScenario<PolicyDistributionCampaign>(owner).PendingEvents[0]);
        Assert.Null(launched.AcknowledgementText);
    }

    [Fact]
    public void ShouldRefuseLaunchGivenOperationWhoseLogicalIdIsNotTheCampaign()
    {
        // Arrange
        var owner = new PolicyDistributionCampaign(Tenant, Campaign);
        var operation = NativeFacts.Root(owner, Uuid.CreateVersion4(), "LaunchPolicyCampaign", null);

        // Act
        var result = owner.LaunchCampaign(operation, Program, PolicySubject, "core_security", NativeFacts.Ref(),
            Uuid.CreateVersion4(), new string('c', 64), Today, Today.AddDays(30), null, 1, operation.Actor,
            Uuid.CreateVersion4(), NativeFacts.Now, NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Empty(new AggregateScenario<PolicyDistributionCampaign>(owner).PendingEvents);
        Assert.False(owner.IsLaunched);
    }

    [Fact]
    public void ShouldRefuseLaunchAndAppendNothingGivenLaunchedCampaign()
    {
        // Arrange
        var owner = Launched();

        // Act
        var result = Launch(owner, PolicySubject, Uuid.CreateVersion4());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Empty(new AggregateScenario<PolicyDistributionCampaign>(owner).PendingEvents);
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldRestoreOpenCampaignGivenValidLaunchHistory()
    {
        // Arrange
        var source = new PolicyDistributionCampaign(Tenant, Campaign);
        var member = Uuid.CreateVersion4();
        Assert.True(Launch(source, PolicySubject, member).IsSuccess);
        var history = NativeFacts.Raised(source);

        // Act
        var replayed = NativeFacts.Replay(new PolicyDistributionCampaign(Tenant, Campaign), history);

        // Assert
        Assert.True(replayed.IsLaunched);
        Assert.False(replayed.IsSealed);
        Assert.False(replayed.IsClosed);
        Assert.Equal(1, replayed.Revision);
        Assert.Equal(0, replayed.Reconciliations);
        Assert.Equal(member, replayed.OwnerMemberId);
        Assert.Equal(Program, replayed.ProgramId);
        Assert.Equal(PolicySubject, replayed.Subject);
        Assert.Equal(history[0].Metadata.EventId, replayed.Launch!.Metadata.EventId);
        Assert.Empty(new AggregateScenario<PolicyDistributionCampaign>(replayed).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseAdmissionGivenUnlaunchedCampaign()
    {
        // Arrange
        var owner = new PolicyDistributionCampaign(Tenant, Campaign);
        var operation = NativeFacts.Root(owner, Uuid.CreateVersion4(), "AcknowledgePolicy", 0);

        // Act
        var prepared = owner.PrepareOpenAdmission(0);
        var admitted = owner.AdmitResult(operation, NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, prepared.Error!.Kind);
        Assert.Equal(RequestErrorKind.NotFound, admitted.Error!.Kind);
        Assert.Empty(new AggregateScenario<PolicyDistributionCampaign>(owner).PendingEvents);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void ShouldRefuseAdmissionGivenStaleExpectedRevision(long expected)
    {
        // Arrange
        var owner = Launched();

        // Act
        var prepared = owner.PrepareOpenAdmission(expected);

        // Assert
        Assert.False(prepared.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, prepared.Error!.Kind);
        Assert.Empty(new AggregateScenario<PolicyDistributionCampaign>(owner).PendingEvents);
        Assert.Equal(1, owner.Revision);
    }

    [Fact]
    public void ShouldAcceptAdmissionPreparationGivenCurrentRevision()
    {
        // Arrange
        var owner = Launched();

        // Act
        var prepared = owner.PrepareOpenAdmission(owner.Revision);

        // Assert
        Assert.True(prepared.IsSuccess);
        Assert.Empty(new AggregateScenario<PolicyDistributionCampaign>(owner).PendingEvents);
    }

    [Theory]
    [InlineData("admit")]
    [InlineData("close")]
    public void ShouldRefuseRootOperationGivenNoExpectedRevision(string command)
    {
        // Arrange
        var owner = Launched();
        var operation = NativeFacts.Root(owner, Uuid.CreateVersion4(), "AcknowledgePolicy", null);

        // Act
        var result = command == "admit"
            ? owner.AdmitResult(operation, NativeFacts.Ref(), NativeFacts.Ref())
            : owner.AcceptClosure(operation, NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Empty(new AggregateScenario<PolicyDistributionCampaign>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseCancelAndCompleteGivenNoPendingClosure()
    {
        // Arrange
        var owner = Launched();
        var operation = NativeFacts.Root(owner, Uuid.CreateVersion4(), "CloseCampaign", 1);

        // Act
        var canceled = owner.CancelClosure(operation, NativeFacts.Ref());
        var completed = owner.CompleteClosure(operation, Today, "Cycle complete", NativeFacts.Ref(),
            operation.Actor, NativeFacts.Now, NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, canceled.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, completed.Error!.Kind);
        Assert.Empty(new AggregateScenario<PolicyDistributionCampaign>(owner).PendingEvents);
        Assert.False(owner.IsClosed);
    }

    [Fact]
    public void ShouldRefuseReconciliationGivenUnlaunchedCampaign()
    {
        // Arrange
        var owner = new PolicyDistributionCampaign(Tenant, Campaign);
        var operation = NativeFacts.Root(owner, Uuid.CreateVersion4(), "ReconcileCampaignAudience", 0);

        // Act
        var result = owner.Reconcile(operation, Uuid.CreateVersion4(), new string('d', 64), Today, NativeFacts.Ref(),
            operation.Actor, NativeFacts.Now, NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.NotFound, result.Error!.Kind);
        Assert.Empty(new AggregateScenario<PolicyDistributionCampaign>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldAdmitResultAtNextRevisionGivenOpenCampaignAndCurrentExpectedRevision()
    {
        // Arrange
        var logicalId = Uuid.CreateVersion4();
        var owner = Finalized(Launched(logicalId));
        var operation = NativeFacts.Root(owner, logicalId, "AcknowledgePolicy", 1);

        // Act
        var result = owner.AdmitResult(operation, NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.True(result.IsSuccess);
        var admitted = Assert.IsType<PolicyParticipantResultAdmitted>(
            Assert.Single(new AggregateScenario<PolicyDistributionCampaign>(owner).PendingEvents));
        Assert.Equal(2, admitted.Revision);
        Assert.Equal(1, admitted.Generation);
        Assert.Equal(2, owner.Revision);
        Assert.Equal(1, owner.AdmissionCount);
        Assert.False(owner.IsSealed);
    }

    [Fact]
    public void ShouldSealAdmissionGivenAcceptedClosure()
    {
        // Arrange
        var logicalId = Uuid.CreateVersion4();
        var owner = Finalized(Launched(logicalId));
        var operation = NativeFacts.Root(owner, logicalId, "CloseCampaign", 1);

        // Act
        var accepted = owner.AcceptClosure(operation, NativeFacts.Ref(), NativeFacts.Ref());
        var afterwards = owner.PrepareOpenAdmission(owner.Revision);
        var lateResult = owner.AdmitResult(
            NativeFacts.Root(owner, Uuid.CreateVersion4(), "AcknowledgePolicy", owner.Revision), NativeFacts.Ref(),
            NativeFacts.Ref());

        // Assert
        Assert.True(accepted.IsSuccess);
        var closure = Assert.IsType<PolicyCampaignClosureAccepted>(
            Assert.Single(new AggregateScenario<PolicyDistributionCampaign>(owner).PendingEvents));
        Assert.Equal(2, closure.Revision);
        Assert.Equal(1, closure.Generation);
        Assert.True(owner.IsSealed);
        Assert.False(owner.IsClosed);
        Assert.NotNull(owner.PendingClosure);
        Assert.Equal(RequestErrorKind.Conflict, afterwards.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, lateResult.Error!.Kind);
        Assert.Equal(0, owner.AdmissionCount);
    }

    [Fact]
    public void ShouldAmendAudienceGivenOpenCampaignAndSelectedRosterSnapshot()
    {
        // Arrange
        var logicalId = Uuid.CreateVersion4();
        var snapshotId = Uuid.CreateVersion4();
        var owner = Finalized(Launched(logicalId, snapshotId));
        var operation = NativeFacts.Root(owner, logicalId, "ReconcileCampaignAudience", 1);

        // Act
        var result = owner.Reconcile(operation, snapshotId, new string('d', 64), Today, NativeFacts.Ref(),
            operation.Actor, NativeFacts.Now, NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.True(result.IsSuccess);
        var amended = Assert.IsType<PolicyCampaignAudienceAmended>(
            Assert.Single(new AggregateScenario<PolicyDistributionCampaign>(owner).PendingEvents));
        Assert.Equal(1, amended.Reconciliation);
        Assert.Equal(snapshotId, amended.RosterSnapshotId);
        Assert.Equal(1, owner.Reconciliations);
        Assert.Equal(2, owner.Revision);
        Assert.Equal(snapshotId, owner.LatestRosterSnapshotId);
    }

    [Fact]
    public void ShouldRefuseReconciliationAndAppendNothingGivenRosterSnapshotThatIsNotTheSelectedOne()
    {
        // Arrange
        var logicalId = Uuid.CreateVersion4();
        var owner = Finalized(Launched(logicalId, Uuid.CreateVersion4()));
        var operation = NativeFacts.Root(owner, logicalId, "ReconcileCampaignAudience", 1);

        // Act
        var result = owner.Reconcile(operation, Uuid.CreateVersion4(), new string('d', 64), Today,
            NativeFacts.Ref(), operation.Actor, NativeFacts.Now, NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Empty(new AggregateScenario<PolicyDistributionCampaign>(owner).PendingEvents);
        Assert.Equal(1, owner.Revision);
        Assert.Equal(0, owner.Reconciliations);
    }

    [Fact]
    public void ShouldRefuseAdmissionAndAppendNothingGivenStaleExpectedRevisionOnAdmitResult()
    {
        // Arrange
        var logicalId = Uuid.CreateVersion4();
        var owner = Finalized(Launched(logicalId));
        var operation = NativeFacts.Root(owner, logicalId, "AcknowledgePolicy", 0);

        // Act
        var result = owner.AdmitResult(operation, NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Empty(new AggregateScenario<PolicyDistributionCampaign>(owner).PendingEvents);
        Assert.Equal(0, owner.AdmissionCount);
    }

    [Fact]
    public void ShouldRefuseAppendGivenOwnerWhoseSelectedLogicalIdIsNotTheOperation()
    {
        // Arrange
        var owner = Finalized(Launched(Uuid.CreateVersion4()));
        var operation = NativeFacts.Root(owner, Uuid.CreateVersion4(), "AcknowledgePolicy", 1);

        // Act
        var result = owner.AdmitResult(operation, NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Empty(new AggregateScenario<PolicyDistributionCampaign>(owner).PendingEvents);
    }

    static Result Launch(PolicyDistributionCampaign owner, CampaignSubject subject, Uuid member)
    {
        var operation = NativeFacts.Root(owner, owner.Id, "LaunchPolicyCampaign", null, member);
        return owner.LaunchCampaign(operation, Program, subject, "core_security", NativeFacts.Ref(),
            Uuid.CreateVersion4(), new string('c', 64), Today, Today.AddDays(30), "Read and acknowledge", 1,
            operation.Actor, member, NativeFacts.Now, NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref());
    }

    static PolicyDistributionCampaign Launched(Uuid selectedLogicalId = default, Uuid selectedRosterSnapshotId = default)
    {
        var source = new PolicyDistributionCampaign(Tenant, Campaign);
        Assert.True(Launch(source, PolicySubject, Uuid.CreateVersion4()).IsSuccess);
        return NativeFacts.Replay(
            new PolicyDistributionCampaign(Tenant, Campaign, selectedLogicalId, selectedRosterSnapshotId),
            NativeFacts.Raised(source));
    }

    static PolicyDistributionCampaign Finalized(PolicyDistributionCampaign owner)
    {
        owner.ReplayCut.Confirm(owner, owner.ReplayCut.Digest);
        return owner;
    }
}
