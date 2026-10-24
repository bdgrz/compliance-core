using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.ControlMappings;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Tests.Features.ControlMappings;

/// <summary>
///     The applicability line of one (program, edition, criterion): at most one pending proposal, only an
///     independent accepted review excludes the criterion, and a withdrawal names the accepted decision.
/// </summary>
public sealed class CriterionApplicabilityTests
{
    static readonly Uuid TenantId = Uuid.CreateVersion4();
    static readonly Uuid ProgramId = Uuid.CreateVersion4();
    static readonly Uuid EditionId = Uuid.CreateVersion4();
    static readonly Uuid ProposerId = Uuid.CreateVersion4();
    static readonly Uuid ReviewerId = Uuid.CreateVersion4();
    static readonly DateTimeOffset Now = NativeFacts.Now;

    static CriterionApplicability Line(string criterion = "CC6.2", Uuid logicalId = default) =>
        new(TenantId, ProgramId, EditionId, criterion, logicalId);

    static ActorReference Actor(Uuid memberId) => ActorReference.ForMember(memberId, "Member");

    static Result Propose(CriterionApplicability line, Uuid decisionId, long expected,
        string rationale = "No accounts are approved.") => line.Propose(
        NativeFacts.Root(line, decisionId, "ProposeCriterionNotApplicable", expected, ProposerId), rationale, ProposerId,
        Actor(ProposerId), Now, NativeFacts.Ref(), NativeFacts.Ref());

    static Result Review(CriterionApplicability line, Uuid decisionId, long expected, string outcome,
        Uuid? reviewer = null, SeparationOfDutiesWaiver? waiver = null) => line.Review(
        NativeFacts.Root(line, Uuid.CreateVersion4(), "ReviewCriterionApplicability", expected, reviewer ?? ReviewerId),
        decisionId, outcome, "Supported.", reviewer ?? ReviewerId, Actor(reviewer ?? ReviewerId), Now.AddMinutes(1),
        waiver, NativeFacts.Ref(), NativeFacts.Ref());

    static Result Withdraw(CriterionApplicability line, Uuid decisionId, long expected) => line.Withdraw(
        NativeFacts.Root(line, Uuid.CreateVersion4(), "WithdrawCriterionNotApplicable", expected, ProposerId),
        decisionId, "Now applies.", Actor(ProposerId), Now.AddMinutes(2), NativeFacts.Ref(), NativeFacts.Ref());

    [Fact]
    public void ShouldExcludeCriterionGivenAcceptedReviewThenRestoreGivenWithdrawal()
    {
        // Arrange
        var line = Line();
        var decisionId = Uuid.CreateVersion4();
        Assert.True(Propose(line, decisionId, 0).IsSuccess);

        // Act
        var review = Review(line, decisionId, 1, "accept");
        var accepted = line.View!;
        var withdrawal = Withdraw(line, decisionId, 2);

        // Assert
        Assert.True(review.IsSuccess);
        Assert.Equal("not_applicable", accepted.Status);
        Assert.Equal(1, accepted.ActiveVersionNumber);
        Assert.True(withdrawal.IsSuccess);
        Assert.Equal("withdrawn", line.View!.Status);
        Assert.Null(line.View.ActiveVersionNumber);
        Assert.Equal(Actor(ProposerId), line.View.Versions[0].WithdrawnBy);
        Assert.Equal(decisionId, line.View.Versions[0].DecisionId);
    }

    [Fact]
    public void ShouldRejectSelfReviewGivenProposerWithoutExactWaiver()
    {
        // Arrange
        var line = Line();
        var decisionId = Uuid.CreateVersion4();
        Assert.True(Propose(line, decisionId, 0).IsSuccess);
        var waiver = Waiver(new SeparationOfDutiesWaiverScope("criterion_applicability", decisionId, decisionId, 1,
            "review"));
        var wrong = Waiver(new SeparationOfDutiesWaiverScope("criterion_applicability", decisionId, decisionId, 9,
            "review"));

        // Act
        var self = Review(line, decisionId, 1, "accept", ProposerId);
        var wrongWaiver = Review(line, decisionId, 1, "accept", ProposerId, wrong);
        var waived = Review(line, decisionId, 1, "accept", ProposerId, waiver);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, self.Error!.Kind);
        Assert.Equal(RequestErrorKind.Forbidden, wrongWaiver.Error!.Kind);
        Assert.True(waived.IsSuccess);
        Assert.Equal(waiver.Id, line.View!.Versions[0].SeparationOfDutiesWaiverId);
    }

    [Fact]
    public void ShouldRejectProposalGivenPendingStaleOrAlreadyNotApplicable()
    {
        // Arrange
        var line = Line();
        var first = Uuid.CreateVersion4();
        Assert.True(Propose(line, first, 0).IsSuccess);

        // Act
        var pending = Propose(line, Uuid.CreateVersion4(), 1, "Other.");
        Assert.True(Review(line, first, 1, "accept").IsSuccess);
        var stale = Propose(line, Uuid.CreateVersion4(), 1, "Stale.");
        var duplicate = Propose(line, Uuid.CreateVersion4(), 2, "Again.");

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, pending.Error!.Kind);
        Assert.Contains("pending", pending.Error.Message, StringComparison.Ordinal);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, duplicate.Error!.Kind);
        Assert.Contains("already not applicable", duplicate.Error.Message, StringComparison.Ordinal);
        Assert.Equal(2, line.Revision);
    }

    [Fact]
    public void ShouldRefuseOldReviewAndWithdrawalGivenAnotherDecisionIsPendingOrAccepted()
    {
        // Arrange
        var line = Line();
        var first = Uuid.CreateVersion4();
        var second = Uuid.CreateVersion4();
        Assert.True(Propose(line, first, 0).IsSuccess);
        Assert.True(Review(line, first, 1, "reject").IsSuccess);
        Assert.True(Propose(line, second, 2).IsSuccess);

        // Act
        var oldReview = Review(line, first, 3, "accept");
        Assert.True(Review(line, second, 3, "accept").IsSuccess);
        var oldWithdrawal = Withdraw(line, first, 4);
        var withdrawal = Withdraw(line, second, 4);
        var afterWithdrawal = Review(line, second, 5, "accept");

        // Assert
        Assert.Equal(RequestErrorKind.Conflict, oldReview.Error!.Kind);
        Assert.Equal(RequestErrorKind.Conflict, oldWithdrawal.Error!.Kind);
        Assert.True(withdrawal.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, afterWithdrawal.Error!.Kind);
        Assert.Equal(["rejected", "withdrawn"], line.View!.Versions.Select(static version => version.Status));
    }

    [Fact]
    public void ShouldKeepCriteriaIndependentGivenDifferentIdentifiers()
    {
        // Arrange
        var accounts = Line("CC6.2");
        var access = Line("CC6.1");

        // Act
        var results = new[] { Propose(accounts, Uuid.CreateVersion4(), 0), Propose(access, Uuid.CreateVersion4(), 0) };

        // Assert
        Assert.All(results, static result => Assert.True(result.IsSuccess));
        Assert.NotEqual(accounts.Id, access.Id);
        Assert.Equal(CriterionApplicability.IdentityOf(TenantId, ProgramId, EditionId, "CC6.2"), accounts.Id);
    }

    [Fact]
    public void ShouldReplayAndProjectTheSameViewGivenReducerOverRaisedFacts()
    {
        // Arrange
        var line = Line();
        var first = Uuid.CreateVersion4();
        var second = Uuid.CreateVersion4();
        Assert.True(Propose(line, first, 0).IsSuccess);
        Assert.True(Review(line, first, 1, "reject").IsSuccess);
        Assert.True(Propose(line, second, 2, "Second attempt.").IsSuccess);
        Assert.True(Review(line, second, 3, "accept").IsSuccess);
        var facts = NativeFacts.Raised(line);

        // Act
        CriterionApplicabilityView? projected = null;
        foreach (var fact in facts)
            projected = CriterionApplicabilityReducer.Apply(projected, fact);
        var replayed = NativeFacts.Replay(Line(), facts);

        // Assert
        Assert.Equal(line.View! with { Versions = [] }, projected! with { Versions = [] });
        Assert.Equal(line.View.Versions, projected.Versions);
        Assert.Equal(line.View with { Versions = [] }, replayed.View! with { Versions = [] });
        Assert.Equal(line.View.Versions, replayed.View.Versions);
        Assert.Equal(["rejected", "accepted"], projected.Versions.Select(static version => version.Status));
        Assert.Equal([first, second], projected.Versions.Select(static version => version.DecisionId));
    }

    static SeparationOfDutiesWaiver Waiver(SeparationOfDutiesWaiverScope scope)
    {
        var waiver = new SeparationOfDutiesWaiver(TenantId, Uuid.CreateVersion4());
        Assert.Null(waiver.Record(scope, ProposerId, Uuid.CreateVersion4(), "Requester", "Sole qualified reviewer.", Now,
            Now.AddDays(1)));
        Assert.Null(waiver.Approve(Uuid.CreateVersion4(), "Approver", Now.AddMinutes(1)));
        return waiver;
    }
}
