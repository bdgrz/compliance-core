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
using Bdgrz.Compliance.Features.SourceFacts.Models;
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
        Assert.Null(pending.OperationResult);

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
        var action = Uuid.CreateVersion4();
        var owner = Raised(action);
        Assert.True(owner.AdmitAction(Op(owner, nameof(AddCorrectiveAction), Author, id: action),
            NativeFacts.Ref(ChildAdmissionIntent.Schema), NativeFacts.Ref()).IsSuccess);
        var source = new NativeFactReference(new CorrectiveAction(owner.TenantId, owner.ProgramId, owner.FindingId, action).Stream,
            Uuid.CreateVersion4(), 2);
        var report = FindingContent.Provenance(Uuid.CreateVersion5(source.EventId, nameof(RecordCorrectiveActionOutcome)),
            nameof(RecordCorrectiveActionOutcome), owner.Stream, ActorReference.ForSystemProcess("report", "Report outcome"),
            null, new string('a', 64), NativeFacts.Now) with { FirstOwner = owner.Stream, CausationEventId = source.EventId };
        Assert.True(owner.RecordActionOutcome(report, action, "completed", source, NativeFacts.Ref(SourceFactProofs.RequiredFactsSchema)).IsSuccess);
        new AggregateScenario<FindingAggregate>(owner).Given();

        // Act
        var result = owner.AcceptClosure(Op(owner, nameof(CloseFinding), Reviewer), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Equal(0, owner.UnresolvedActionCount);
        Assert.Null(owner.PendingClosure);
        Assert.Empty(new AggregateScenario<FindingAggregate>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldAllowFindingAuthorToReviewGivenTheyAuthoredNoClosureProposalRevision()
    {
        // Arrange
        var owner = Raised();
        var closure = Uuid.CreateVersion4();
        Assert.True(Propose(owner, closure, 0, Other).IsSuccess);

        // Act
        var result = Review(owner, closure, 1, Author, true);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(Author, owner.PendingClosure!.Review!.ReviewedBy);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ShouldRecordExactWaiverGivenProposalAuthorReviewsCurrentRevision(bool approve)
    {
        // Arrange
        var owner = Raised();
        var closure = Uuid.CreateVersion4();
        Assert.True(Propose(owner, closure, 0, Author).IsSuccess);
        var waiver = Waiver(owner, closure, 1, Author, approve);

        // Act
        var result = Review(owner, closure, 1, Author, approve, waiver);

        // Assert
        Assert.True(result.IsSuccess);
        var binding = approve ? owner.PendingClosure!.Review : Assert.IsType<FindingClosureReturned>(owner.Latest).Review;
        Assert.Equal(waiver, binding!.Waiver);
        Assert.Equal(closure, binding.ClosureId);
        Assert.Equal(1, binding.ProposalRevision);
        Assert.Equal(Uuid.Parse(Author.Id), binding.ReviewerMemberId);
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("beneficiary")]
    [InlineData("reviewer")]
    [InlineData("finding")]
    [InlineData("proposal")]
    [InlineData("revision")]
    [InlineData("action")]
    [InlineData("approval-source")]
    [InlineData("approval-event")]
    [InlineData("expired")]
    [InlineData("not-yet-valid")]
    public void ShouldRefuseWithoutFactGivenWaiverDoesNotCoverCurrentReview(string difference)
    {
        // Arrange
        var owner = Raised();
        var closure = Uuid.CreateVersion4();
        Assert.True(Propose(owner, closure, 0, Author).IsSuccess);
        var waiver = Waiver(owner, closure, 1, Author, true);
        waiver = difference switch
        {
            "tenant" => waiver with { TenantId = Uuid.CreateVersion4() },
            "beneficiary" => waiver with { SubjectId = Uuid.CreateVersion4() },
            "reviewer" => waiver with { ActorId = Uuid.CreateVersion4() },
            "finding" => waiver with { TargetId = Uuid.CreateVersion4() },
            "proposal" => waiver with { TargetVersionId = Uuid.CreateVersion4() },
            "revision" => waiver with { TargetRevision = 2 },
            "action" => waiver with { Action = SeparationOfDutiesActions.Review },
            "approval-source" => waiver with { ApprovalFact = waiver.ApprovalFact with { Source = owner.Stream } },
            "approval-event" => waiver with { ApprovalFact = waiver.ApprovalFact with { EventId = Uuid.Empty } },
            "expired" => waiver with { ValidUntil = NativeFacts.Now },
            "not-yet-valid" => waiver with { ValidFrom = NativeFacts.Now.AddSeconds(1) },
            _ => throw new ArgumentOutOfRangeException(nameof(difference)),
        };
        new AggregateScenario<FindingAggregate>(owner).Given();

        // Act
        var result = Review(owner, closure, 1, Author, true, waiver);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Null(owner.PendingClosure);
        Assert.Empty(new AggregateScenario<FindingAggregate>(owner).PendingEvents);
    }

    [Theory]
    [InlineData("evidence")]
    [InlineData("artifact-version")]
    [InlineData("verification")]
    [InlineData("rationale")]
    public void ShouldRefuseProposalWithoutFactGivenMissingResolutionTerms(string missing)
    {
        // Arrange
        var owner = Raised();
        var terms = Terms();
        terms = missing switch
        {
            "evidence" => terms with { ResolutionEvidence = [] },
            "artifact-version" => terms with { ResolutionEvidence = [new(Uuid.CreateVersion4(), "unversioned")] },
            "verification" => terms with { VerificationRationale = " " },
            "rationale" => terms with { Rationale = " " },
            _ => throw new ArgumentOutOfRangeException(nameof(missing)),
        };
        new AggregateScenario<FindingAggregate>(owner).Given();

        // Act
        var result = owner.ProposeClosure(Op(owner, nameof(ProposeFindingClosure), Author), Uuid.CreateVersion4(), 0,
            terms, Actions(owner), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.Null(owner.ClosureProposal);
        Assert.Empty(new AggregateScenario<FindingAggregate>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseReviewWithoutFactGivenStaleFindingRevision()
    {
        // Arrange
        var owner = Raised();
        var before = owner.Revision;
        var closure = Uuid.CreateVersion4();
        Assert.True(Propose(owner, closure, 0, Author).IsSuccess);
        new AggregateScenario<FindingAggregate>(owner).Given();

        // Act
        var result = owner.ReviewClosure(Op(owner, nameof(ReviewFindingClosure), Reviewer, before), closure, 1,
            true, null, null, NativeFacts.Ref(ClosureIntent.Schema), NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Empty(new AggregateScenario<FindingAggregate>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldRetainAuthorExclusionGivenProposalLineageReplayed()
    {
        // Arrange
        var owner = Raised();
        var closure = Uuid.CreateVersion4();
        Assert.True(Propose(owner, closure, 0, Author).IsSuccess);
        Assert.True(Review(owner, closure, 1, Reviewer, false).IsSuccess);
        Assert.True(Propose(owner, closure, 1, Other).IsSuccess);
        var facts = NativeFacts.Raised(owner);

        // Act
        var replayed = NativeFacts.Replay(new FindingAggregate(owner.TenantId, owner.ProgramId, owner.FindingId), facts);
        var result = Review(replayed, closure, 2, Author, true);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, result.Error?.Kind);
        Assert.Equal(2, replayed.ClosureProposal!.ProposalRevision);
        Assert.Empty(new AggregateScenario<FindingAggregate>(replayed).PendingEvents);
    }

    [Theory]
    [InlineData("proposal")]
    [InlineData("revision")]
    [InlineData("author")]
    [InlineData("expected-revision")]
    [InlineData("intent")]
    public void ShouldRefuseCorruptAcceptedReviewGivenOwnerReplay(string difference)
    {
        // Arrange
        var owner = Raised();
        var closure = Uuid.CreateVersion4();
        Assert.True(Propose(owner, closure, 0, Author).IsSuccess);
        Assert.True(Review(owner, closure, 1, Reviewer, true).IsSuccess);
        var facts = NativeFacts.Raised(owner);
        var accepted = Assert.IsType<FindingClosureAccepted>(facts[^1]);
        facts[^1] = difference switch
        {
            "proposal" => accepted with { Review = accepted.Review! with { ClosureId = Uuid.CreateVersion4() } },
            "revision" => accepted with { Review = accepted.Review! with { ProposalRevision = 2 } },
            "author" => accepted with
            {
                Review = accepted.Review! with { ReviewerMemberId = Uuid.Parse(Author.Id), ReviewedBy = Author },
                Operation = accepted.Operation with { Actor = Author },
            },
            "expected-revision" => accepted with { Operation = accepted.Operation with { OriginalExpectedRevision = 0 } },
            "intent" => accepted with { Intent = NativeFacts.Ref("other-intent.v1") },
            _ => throw new ArgumentOutOfRangeException(nameof(difference)),
        };

        // Act
        var refusal = Assert.Throws<InvalidOperationException>(() => NativeFacts.Replay(
            new FindingAggregate(owner.TenantId, owner.ProgramId, owner.FindingId), facts));

        // Assert
        Assert.Contains("closure is accepted", refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldPreserveEarlierClosureAndRequireNewReviewGivenReasonedReopen()
    {
        // Arrange
        var owner = Raised();
        var closure = Uuid.CreateVersion4();
        Assert.True(Propose(owner, closure, 0, Author).IsSuccess);
        Assert.True(Review(owner, closure, 1, Reviewer, true).IsSuccess);
        Assert.True(Finish(owner, owner.PendingClosure!).IsSuccess);
        var closed = owner.Closed!;

        // Act
        var result = owner.Reopen(Op(owner, nameof(ReopenFinding), Reviewer), " New evidence disproved verification. ",
            Reviewer, NativeFacts.Now, NativeFacts.Ref(), NativeFacts.Ref());
        var reopened = Assert.IsType<FindingReopenedV2>(owner.Latest);
        var reproposed = Propose(owner, closure, 1, Other);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(reproposed.IsSuccess);
        Assert.Equal("New evidence disproved verification.", reopened.Reason);
        Assert.Equal(closure, reopened.PreviousClosureId);
        Assert.Equal(1, reopened.PreviousProposalRevision);
        Assert.Equal(closure, closed.Review!.ClosureId);
        Assert.Equal(2, owner.Generation);
        Assert.Equal(2, owner.ClosureProposal!.ProposalRevision);
        Assert.Null(owner.Closed);
        Assert.Null(owner.PendingClosure);
    }

    [Fact]
    public void ShouldRefuseReopenWithoutFactGivenNoReason()
    {
        // Arrange
        var owner = Raised();
        var closure = Uuid.CreateVersion4();
        Assert.True(Propose(owner, closure, 0, Author).IsSuccess);
        Assert.True(Review(owner, closure, 1, Reviewer, true).IsSuccess);
        Assert.True(Finish(owner, owner.PendingClosure!).IsSuccess);
        new AggregateScenario<FindingAggregate>(owner).Given();

        // Act
        var result = owner.Reopen(Op(owner, nameof(ReopenFinding), Reviewer), " ", Reviewer, NativeFacts.Now,
            NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.NotNull(owner.Closed);
        Assert.Empty(new AggregateScenario<FindingAggregate>(owner).PendingEvents);
    }

    [Fact]
    public void ShouldRefuseDelayedCompletionGivenEarlierReviewCanceledAndProposalReplaced()
    {
        // Arrange
        var owner = Raised();
        var closure = Uuid.CreateVersion4();
        Assert.True(Propose(owner, closure, 0, Author).IsSuccess);
        Assert.True(Review(owner, closure, 1, Reviewer, true).IsSuccess);
        var earlier = owner.PendingClosure!;
        Assert.True(owner.CancelClosure(earlier.Operation with { LogicalId = Uuid.CreateVersion4() }, NativeFacts.Ref()).IsSuccess);
        Assert.True(owner.WithdrawClosure(Op(owner, nameof(WithdrawFindingClosure), Author), closure, 1,
            NativeFacts.Ref(), NativeFacts.Ref()).IsSuccess);
        Assert.True(Propose(owner, closure, 1, Other).IsSuccess);
        Assert.True(Review(owner, closure, 2, Reviewer, true).IsSuccess);
        var current = owner.PendingClosure;
        new AggregateScenario<FindingAggregate>(owner).Given();

        // Act
        var result = Finish(owner, earlier);

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, result.Error?.Kind);
        Assert.Same(current, owner.PendingClosure);
        Assert.Null(owner.Closed);
        Assert.Empty(new AggregateScenario<FindingAggregate>(owner).PendingEvents);
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
        Op(owner, nameof(ProposeFindingClosure), actor), closure, prior, Terms(), Actions(owner), NativeFacts.Ref(), NativeFacts.Ref());

    static FindingClosureTerms Terms() => new([new FindingClosureArtifactVersion(Uuid.CreateVersion4(), new string('a', 64))],
        "Verified removal.", "Resolution is complete.");

    static ImmutableSelectionManifest Actions(FindingAggregate owner) => new(NativeFacts.Ref(), FindingContent.AdmittedChildSchema,
        owner.AdmittedCount, SelectionOrder.CanonicalKey, new string('b', 64));

    static Result Finish(FindingAggregate owner, FindingClosureAccepted pending) => owner.CompleteClosure(pending.Operation with
    {
        LogicalId = Uuid.CreateVersion4(), CommandKind = nameof(CompleteFindingClosure),
    }, NativeFacts.Ref(), NativeFacts.Ref(), pending.Intent, NativeFacts.Ref());

    static SeparationOfDutiesWaiverProof Waiver(FindingAggregate owner, Uuid closure, long revision,
        ActorReference actor, bool approve)
    {
        var id = Uuid.CreateVersion4();
        var member = Uuid.Parse(actor.Id);
        return new SeparationOfDutiesWaiverProof(owner.TenantId, id, member, member,
            approve ? SeparationOfDutiesActions.Approve : SeparationOfDutiesActions.Review,
            SeparationOfDutiesRecordTypes.Finding, owner.FindingId, closure, revision,
            new SourceFactLocator(new EventStreamAddress(owner.TenantId.ToString(), "separation-of-duties-waivers", id.ToString()),
                Uuid.CreateVersion4(), 2, 2, 2, 2, new string('d', 64)),
            NativeFacts.Now.AddHours(-1), NativeFacts.Now.AddHours(1));
    }

    static Result Review(FindingAggregate owner, Uuid closure, long revision, ActorReference actor, bool approve,
        SeparationOfDutiesWaiverProof? waiver = null) => owner.ReviewClosure(Op(owner, nameof(ReviewFindingClosure), actor),
            closure, revision, approve, approve ? null : "Please clarify verification.", waiver,
            approve ? NativeFacts.Ref(ClosureIntent.Schema) : null, NativeFacts.Ref(), NativeFacts.Ref());

    static OperationProvenance Op(FindingAggregate owner, string kind, ActorReference actor, long? revision = -1,
        Uuid id = default) => FindingContent.Provenance(id == Uuid.Empty ? Uuid.CreateVersion4() : id, kind,
        owner.Stream, actor, revision == -1 ? (ulong)owner.Revision : revision is { } expected ? (ulong)expected : null,
        new string('c', 64), NativeFacts.Now) with { FirstOwner = owner.Stream };
}
