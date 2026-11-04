using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Tests.Testing;
namespace Bdgrz.Compliance.Tests.Features.Remediation.Tests;

public sealed class FindingClosureProposalTests
{
    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Author");
    static readonly ActorReference Other = ActorReference.ForMember(Uuid.CreateVersion4(), "Other author");
    static readonly ActorReference Reviewer = ActorReference.ForMember(Uuid.CreateVersion4(), "Reviewer");

    [Fact]
    public void ShouldProposeClosureGivenZeroPlannedActionsAndResolutionEvidence()
    {
        // Arrange
        var owner = Raised();
        var closure = Uuid.CreateVersion4();

        // Act
        var result = Propose(owner, closure, 0, Author);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(closure, owner.ClosureProposal!.ClosureId);
        Assert.Equal(1, owner.ClosureProposal.ProposalRevision);
        Assert.Equal(0, owner.ClosureProposal.ActionsReliedOn.Count);
        Assert.Null(owner.Closed);
    }

    [Fact]
    public void ShouldRefuseProposalWithoutFactGivenUnresolvedActionInOwnStream()
    {
        // Arrange
        var action = Uuid.CreateVersion4();
        var owner = Raised(action);
        Assert.True(owner.AdmitAction(Op(owner, nameof(AddCorrectiveAction), Author),
            NativeFacts.Ref(ChildAdmissionIntent.Schema), NativeFacts.Ref()).IsSuccess);
        new AggregateScenario<FindingAggregate>(owner).Given();

        // Act
        var result = Propose(owner, Uuid.CreateVersion4(), 0, Author);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Null(owner.ClosureProposal);
        Assert.Empty(new AggregateScenario<FindingAggregate>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldSealActionAdmissionGivenProposalAwaitingReview()
    {
        // Arrange
        var owner = Raised();
        Assert.True(Propose(owner, Uuid.CreateVersion4(), 0, Author).IsSuccess);
        new AggregateScenario<FindingAggregate>(owner).Given();

        // Act
        var result = owner.AdmitAction(Op(owner, nameof(AddCorrectiveAction), Author),
            NativeFacts.Ref(ChildAdmissionIntent.Schema), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<FindingAggregate>(owner).PendingEvents);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ShouldRefuseSelfReviewWithoutFactGivenProposalAuthor(bool approve)
    {
        // Arrange
        var owner = Raised();
        var closure = Uuid.CreateVersion4();
        Assert.True(Propose(owner, closure, 0, Author).IsSuccess);
        new AggregateScenario<FindingAggregate>(owner).Given();

        // Act
        var result = Review(owner, closure, 1, Author, approve);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<FindingAggregate>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldRetainEveryAuthorGivenReturnedProposalRevisedByAnotherMember()
    {
        // Arrange
        var owner = Raised();
        var closure = Uuid.CreateVersion4();
        Assert.True(Propose(owner, closure, 0, Author).IsSuccess);
        Assert.True(Review(owner, closure, 1, Reviewer, false).IsSuccess);
        Assert.True(Propose(owner, closure, 1, Other).IsSuccess);
        new AggregateScenario<FindingAggregate>(owner).Given();
        var renamed = Author with { Id = Author.Id.ToUpperInvariant(), Display = "Renamed" };

        // Act
        var result = Review(owner, closure, 2, renamed, true);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(2, owner.ClosureProposal!.ProposalRevision);
        Assert.Empty(new AggregateScenario<FindingAggregate>(owner).PendingEvents);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ShouldRefuseWithoutFactGivenReviewNamesStaleProposal(bool changedId)
    {
        // Arrange
        var owner = Raised();
        var closure = Uuid.CreateVersion4();
        Assert.True(Propose(owner, closure, 0, Author).IsSuccess);
        new AggregateScenario<FindingAggregate>(owner).Given();

        // Act
        var result = Review(owner, changedId ? Uuid.CreateVersion4() : closure, changedId ? 1 : 2, Reviewer, true);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<FindingAggregate>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldPermitWithdrawalOnlyByProposerGivenCurrentProposal()
    {
        // Arrange
        var owner = Raised();
        var closure = Uuid.CreateVersion4();
        Assert.True(Propose(owner, closure, 0, Author).IsSuccess);

        // Act
        var foreign = owner.WithdrawClosure(Op(owner, nameof(WithdrawFindingClosure), Other), closure, 1,
            NativeFacts.Ref(), NativeFacts.Ref());
        var own = owner.WithdrawClosure(Op(owner, nameof(WithdrawFindingClosure), Author), closure, 1,
            NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, foreign.Error?.Kind);
        Assert.True(own.IsSuccess);
        Assert.Null(owner.ClosureProposal);
        Assert.IsType<FindingClosureWithdrawn>(new AggregateScenario<FindingAggregate>(owner).PendingEvents[^1]);
    }

    [Fact]
    public void ShouldKeepFindingOpenAndProposalReturnedGivenIndependentReturn()
    {
        // Arrange
        var owner = Raised();
        var closure = Uuid.CreateVersion4();
        Assert.True(Propose(owner, closure, 0, Author).IsSuccess);

        // Act
        var result = Review(owner, closure, 1, Reviewer, false);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(owner.ClosureProposal);
        Assert.Null(owner.PendingClosure);
        Assert.Null(owner.Closed);
        Assert.Equal("Please clarify verification.", Assert.IsType<FindingClosureReturned>(
            new AggregateScenario<FindingAggregate>(owner).PendingEvents[^1]).Comments);
    }

    [Fact]
    public void ShouldBindApprovalAndFinalClosureGivenIndependentCurrentReview()
    {
        // Arrange
        var owner = Raised();
        var closure = Uuid.CreateVersion4();
        Assert.True(Propose(owner, closure, 0, Author).IsSuccess);
        Assert.True(Review(owner, closure, 1, Reviewer, true).IsSuccess);
        var pending = owner.PendingClosure!;

        // Act
        var result = owner.CompleteClosure(pending.Operation with { LogicalId = Uuid.CreateVersion4() },
            NativeFacts.Ref(), NativeFacts.Ref(), pending.Intent, NativeFacts.Ref());

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(closure, owner.Closed!.Review!.ClosureId);
        Assert.Equal(1, owner.Closed.Review.ProposalRevision);
        Assert.Equal(Reviewer, owner.Closed.Review.ReviewedBy);
        Assert.Null(owner.ClosureProposal);
    }

    [Fact]
    public void ShouldRefuseLegacyCombinedCloseGivenNoReviewedProposal()
    {
        // Arrange
        var owner = Raised();

        // Act
        var result = owner.AcceptClosure(Op(owner, nameof(CloseFinding), Reviewer), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Null(owner.PendingClosure);
    }

    static FindingAggregate Raised(Uuid action = default)
    {
        var owner = new FindingAggregate(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(), action);
        var terms = new FindingRaiseTerms(new FindingSource("manual", null, null, "Original wording."),
            "Finding", "Description", "high", "VPN", Uuid.CreateVersion4(), new DateOnly(2026, 12, 1),
            Author, NativeFacts.Now, "manual", null);
        Assert.True(owner.Raise(Op(owner, nameof(RaiseFinding), Author, null, owner.FindingId), terms,
            NativeFacts.Ref(), NativeFacts.Ref(), NativeFacts.Ref(), null).IsSuccess);
        return owner;
    }

    static Result Propose(FindingAggregate owner, Uuid closure, long prior, ActorReference actor) => owner.ProposeClosure(
        Op(owner, nameof(ProposeFindingClosure), actor), closure, prior,
        new FindingClosureTerms([new FindingClosureArtifactVersion(Uuid.CreateVersion4(), new string('a', 64))],
            "Verified removal.", "Resolution is complete."),
        new ImmutableSelectionManifest(NativeFacts.Ref(), FindingContent.AdmittedChildSchema, owner.AdmittedCount,
            SelectionOrder.CanonicalKey, new string('b', 64)), NativeFacts.Ref(), NativeFacts.Ref());

    static Result Review(FindingAggregate owner, Uuid closure, long revision, ActorReference actor, bool approve,
        SeparationOfDutiesWaiverProof? waiver = null) => owner.ReviewClosure(Op(owner, nameof(ReviewFindingClosure), actor),
            closure, revision, approve, approve ? null : "Please clarify verification.", waiver,
            approve ? NativeFacts.Ref(ClosureIntent.Schema) : null, NativeFacts.Ref(), NativeFacts.Ref());

    static OperationProvenance Op(FindingAggregate owner, string kind, ActorReference actor, long? revision = -1,
        Uuid id = default) => FindingContent.Provenance(id == Uuid.Empty ? Uuid.CreateVersion4() : id, kind,
        owner.Stream, actor, revision == -1 ? (ulong)owner.Revision : revision is { } expected ? (ulong)expected : null,
        new string('c', 64), NativeFacts.Now) with { FirstOwner = owner.Stream };
}
