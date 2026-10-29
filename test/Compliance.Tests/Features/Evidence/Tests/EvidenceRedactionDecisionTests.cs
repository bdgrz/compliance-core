using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Events;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Evidence.Tests;

/// <summary>Aggregate-level redaction decisions: refusals append nothing, replay of valid history, waiver admission.</summary>
public sealed class EvidenceRedactionDecisionTests
{
    static readonly Uuid Tenant = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
    static readonly Uuid PreparerId = Uuid.CreateVersion4();
    static readonly Uuid LeadId = Uuid.CreateVersion4();
    static readonly ActorReference Preparer = ActorReference.ForMember(PreparerId, "Contributor");
    static readonly ActorReference Lead = ActorReference.ForMember(LeadId, "Lead");

    [Fact]
    public void ShouldReplayPreparationThenIndependentApprovalGivenValidHistory()
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var preparation = Preparation(1);
        var approval = Approval(2, preparation, Lead, null);

        // Act
        var owner = Replay(id, Prepared(id, 0, preparation), Approved(id, 1, approval));

        // Assert
        Assert.Equal(2, owner.Revision);
        Assert.Equal(preparation, owner.CurrentPreparation);
        Assert.Equal(approval, owner.CurrentApproval);
    }

    [Fact]
    public void ShouldClearCurrentApprovalGivenLaterPreparationInReplay()
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var first = Preparation(1);
        var approval = Approval(2, first, Lead, null);
        var second = first with
        {
            PreparationId = Uuid.CreateVersion4(),
            Revision = 3,
            Reason = "Revised",
            PreparedAt = Now.AddMinutes(10)
        };

        // Act
        var owner = Replay(id, Prepared(id, 0, first), Approved(id, 1, approval), Prepared(id, 2, second));

        // Assert
        Assert.Equal(3, owner.Revision);
        Assert.Equal(second, owner.CurrentPreparation);
        Assert.Null(owner.CurrentApproval);
    }

    [Fact]
    public void ShouldAcceptNextRevisionGivenSameSourcesAndChangedReason()
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var first = Preparation(1);
        var owner = Replay(id, Prepared(id, 0, first));

        // Act
        var result = owner.DecidePrepare(Uuid.CreateVersion4(), 1, first.Original, first.Derived, "Manual", "Revised reason",
            Preparer, Now.AddMinutes(5));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Revision);
        Assert.Equal("Revised reason", result.Value.Reason);
    }

    [Theory]
    [InlineData("stale_revision")]
    [InlineData("changed_derived_source")]
    [InlineData("changed_original_source")]
    [InlineData("earlier_than_last_change")]
    public void ShouldRefusePreparationWithoutFactGivenConflictingState(string scenario)
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var first = Preparation(1);
        var owner = Replay(id, Prepared(id, 0, first));
        var scope = new AggregateScenario<EvidenceRedaction>(owner);
        var expected = scenario == "stale_revision" ? 0 : 1;
        var original = scenario == "changed_original_source" ? Identity('e') : first.Original;
        var derived = scenario == "changed_derived_source" ? Identity('f') : first.Derived;
        var at = scenario == "earlier_than_last_change" ? Now.AddMinutes(-1) : Now.AddMinutes(5);

        // Act
        var result = owner.DecidePrepare(Uuid.CreateVersion4(), expected, original, derived, "Manual", "Different", Preparer, at);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Empty(scope.PendingEvents);
        Assert.Equal(1, owner.Revision);
    }

    [Theory]
    [InlineData("same_artifact")]
    [InlineData("same_content")]
    [InlineData("empty_reason")]
    [InlineData("non_member_actor")]
    public void ShouldRefusePreparationWithoutFactGivenInvalidDerivativeLineage(string scenario)
    {
        // Arrange
        var owner = new EvidenceRedaction(Tenant, Uuid.CreateVersion4());
        var scope = new AggregateScenario<EvidenceRedaction>(owner);
        var original = Identity('a');
        var derived = scenario switch
        {
            "same_artifact" => Identity('b') with { ArtifactId = original.ArtifactId },
            "same_content" => original with { ArtifactId = Uuid.CreateVersion4() },
            _ => Identity('b')
        };
        derived = derived with { IdentitySha256 = EvidenceRedaction.IdentityDigest(derived with { IdentitySha256 = "" }) };
        var actor = scenario == "non_member_actor" ? ActorReference.ForPlatformOperator(PreparerId, "Operator") : Preparer;
        var reason = scenario == "empty_reason" ? "  " : "Private fields";

        // Act
        var result = owner.DecidePrepare(Uuid.CreateVersion4(), 0, original, derived, "Manual", reason, actor, Now);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, result.Error!.Kind);
        Assert.Empty(scope.PendingEvents);
        Assert.Equal(0, owner.Revision);
    }

    [Theory]
    [InlineData("stale_revision")]
    [InlineData("other_preparation")]
    [InlineData("other_prepared_revision")]
    [InlineData("source_position_below_two")]
    [InlineData("available_after_decision")]
    public void ShouldRefuseApprovalWithoutFactGivenAnythingButCurrentExactPreparation(string scenario)
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var preparation = Preparation(1);
        var owner = Replay(id, Prepared(id, 0, preparation));
        var scope = new AggregateScenario<EvidenceRedaction>(owner);
        var expected = scenario == "stale_revision" ? 0 : 1;
        var preparationId = scenario == "other_preparation" ? Uuid.CreateVersion4() : preparation.PreparationId;
        var preparedRevision = scenario == "other_prepared_revision" ? 2 : preparation.Revision;
        var position = scenario == "source_position_below_two" ? 1UL : 2UL;
        var availableAt = scenario == "available_after_decision" ? Now.AddHours(2) : Now.AddMinutes(1);

        // Act
        var result = owner.DecideApprove(Uuid.CreateVersion4(), expected, preparationId, preparedRevision, position, Lead,
            Now.AddMinutes(2), availableAt, null);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Empty(scope.PendingEvents);
        Assert.Null(owner.CurrentApproval);
    }

    [Fact]
    public void ShouldRefuseApprovalWithoutFactGivenNoPreparation()
    {
        // Arrange
        var owner = new EvidenceRedaction(Tenant, Uuid.CreateVersion4());
        var scope = new AggregateScenario<EvidenceRedaction>(owner);

        // Act
        var result = owner.DecideApprove(Uuid.CreateVersion4(), 0, Uuid.CreateVersion4(), 1, 2, Lead, Now, Now, null);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Empty(scope.PendingEvents);
    }

    [Fact]
    public void ShouldRefuseSecondApprovalGivenCurrentPreparationAlreadyApproved()
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var preparation = Preparation(1);
        var owner = Replay(id, Prepared(id, 0, preparation), Approved(id, 1, Approval(2, preparation, Lead, null)));

        // Act
        var result = owner.DecideApprove(Uuid.CreateVersion4(), 2, preparation.PreparationId, 1, 2, Lead, Now.AddMinutes(5),
            Now.AddMinutes(1), null);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Equal(2, owner.Revision);
    }

    [Fact]
    public void ShouldForbidSelfApprovalGivenPreparerWithoutWaiver()
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var preparation = Preparation(1);
        var owner = Replay(id, Prepared(id, 0, preparation));

        // Act
        var result = owner.DecideApprove(Uuid.CreateVersion4(), 1, preparation.PreparationId, 1, 2, Preparer, Now.AddMinutes(2),
            Now.AddMinutes(1), null);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error!.Kind);
        Assert.Null(owner.CurrentApproval);
    }

    [Fact]
    public void ShouldAcceptSelfApprovalGivenExactScopedIndependentWaiver()
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var preparation = Preparation(1);
        var owner = Replay(id, Prepared(id, 0, preparation));
        var waiver = Waiver(owner, preparation, PreparerId, LeadId);

        // Act
        var result = owner.DecideApprove(Uuid.CreateVersion4(), 1, preparation.PreparationId, 1, 2, Preparer, Now.AddMinutes(2),
            Now.AddMinutes(1), waiver);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(waiver, result.Value.SeparationOfDutiesWaiver);
        Assert.Equal(2, result.Value.Revision);
    }

    [Theory]
    [InlineData("other_preparation_scope")]
    [InlineData("approver_is_beneficiary")]
    [InlineData("beneficiary_is_another_member")]
    [InlineData("approver_is_requester")]
    [InlineData("requested_before_preparation")]
    [InlineData("approved_after_decision")]
    [InlineData("expired")]
    [InlineData("other_tenant")]
    public void ShouldForbidSelfApprovalGivenWaiverThatIsNotExactIndependentAndActive(string scenario)
    {
        // Arrange
        var id = Uuid.CreateVersion4();
        var preparation = Preparation(1);
        var owner = Replay(id, Prepared(id, 0, preparation));
        var valid = Waiver(owner, preparation, PreparerId, LeadId);
        var waiver = scenario switch
        {
            "other_preparation_scope" => valid with { Scope = valid.Scope with { Revision = 9 } },
            "approver_is_beneficiary" => valid with { ApproverMemberId = PreparerId },
            "beneficiary_is_another_member" => valid with { BeneficiaryMemberId = Uuid.CreateVersion4() },
            "approver_is_requester" => valid with { RequesterMemberId = LeadId },
            "requested_before_preparation" => valid with { RequestedAt = Now.AddMinutes(-1) },
            "approved_after_decision" => valid with { ApprovedAt = Now.AddMinutes(3) },
            "expired" => valid with { ExpiresAt = Now.AddMinutes(2) },
            _ => valid with { TenantId = Uuid.CreateVersion4() }
        };

        // Act
        var result = owner.DecideApprove(Uuid.CreateVersion4(), 1, preparation.PreparationId, 1, 2, Preparer, Now.AddMinutes(2),
            Now.AddMinutes(1), waiver);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error!.Kind);
        Assert.Null(owner.CurrentApproval);
    }

    static EvidenceRedaction Replay(Uuid id, params DomainEvent[] facts) =>
        new AggregateScenario<EvidenceRedaction>(new EvidenceRedaction(Tenant, id)).Given(facts).Aggregate;

    static EvidenceRedactionPrepared Prepared(Uuid id, long expected, EvidenceRedactionPreparationFact value) =>
        DomainEventSeed.Attach(new EvidenceRedactionPrepared(Tenant, id, expected, value), id,
            (ulong)value.Revision, occurredOn: value.PreparedAt);

    static EvidenceRedactionApproved Approved(Uuid id, long expected, EvidenceRedactionApprovalFact value) =>
        DomainEventSeed.Attach(new EvidenceRedactionApproved(Tenant, id, expected, value), id,
            (ulong)value.Revision, occurredOn: value.ApprovedAt);

    static EvidenceRedactionPreparationFact Preparation(long revision) =>
        new(Uuid.CreateVersion4(), revision, Identity('a'), Identity('b'), "Manual", "Private fields", Preparer, Now);

    static EvidenceRedactionApprovalFact Approval(long revision, EvidenceRedactionPreparationFact prepared, ActorReference by,
        EvidenceRedactionWaiverSnapshot? waiver) =>
        new(Uuid.CreateVersion4(), revision, prepared.PreparationId, prepared.Revision, 2, Now.AddMinutes(1), by,
            Now.AddMinutes(2), waiver);

    static EvidenceRedactionWaiverSnapshot Waiver(EvidenceRedaction owner, EvidenceRedactionPreparationFact prepared,
        Uuid beneficiary, Uuid approver) =>
        new(Tenant, Uuid.CreateVersion4(), owner.WaiverScope(prepared), beneficiary, beneficiary, approver, Now.AddMinutes(1),
            Now.AddMinutes(1), Now.AddHours(1));

    static EvidenceRedactionSourceCapsule Identity(char hash)
    {
        var digest = new string(hash, 64);
        var identity = new EvidenceRedactionSourceCapsule(Tenant, EvidenceIntake.IdFor(Tenant, digest), Uuid.CreateVersion4(),
            new string('c', 64), digest, 123, Now.AddDays(-1), "");
        return identity with { IdentitySha256 = EvidenceRedaction.IdentityDigest(identity) };
    }
}
