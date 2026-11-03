using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Commands;
using Bdgrz.Compliance.Features.Risks.Events;
using Bdgrz.Compliance.Features.Risks.Services;
using Bdgrz.Compliance.Tests.Testing;

namespace Bdgrz.Compliance.Tests.Features.Risks.Tests;

public sealed class RiskTreatmentAssertionAggregateTests
{
    [Theory]
    [InlineData("accept", false)]
    [InlineData("reject", false)]
    [InlineData("accept", true)]
    [InlineData("reject", true)]
    public void ShouldRefuseReviewWithoutFactGivenProposerWithoutWaiverReference(string outcome,
        bool emptyWaiver)
    {
        // Arrange
        var proposer = Uuid.CreateVersion4();
        var reviewId = Uuid.CreateVersion4();
        var owner = PendingProposal(proposer, reviewId);
        var scenario = new AggregateScenario<RiskTreatmentAssertion>(owner);
        var treatmentId = owner.Pending!.TreatmentId;
        var operation = NativeFacts.Root(owner, reviewId, nameof(ReviewRiskControlTreatment), 1, proposer);
        Uuid? waiver = emptyWaiver ? Uuid.Empty : null;

        // Act
        var result = owner.Review(operation, treatmentId, outcome, "Review the proposed treatment.",
            operation.Actor, operation.AcceptedAt, waiver, NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Forbidden, result.Error!.Kind);
        Assert.Empty(scenario.PendingEvents);
        Assert.Equal(1, owner.Revision);
        Assert.Equal(treatmentId, owner.Pending!.TreatmentId);
        Assert.Null(owner.Accepted);
    }

    [Theory]
    [InlineData("accept")]
    [InlineData("reject")]
    public void ShouldRecordReviewGivenIndependentReviewerWithoutWaiver(string outcome)
    {
        // Arrange
        var reviewId = Uuid.CreateVersion4();
        var owner = PendingProposal(Uuid.CreateVersion4(), reviewId);
        var scenario = new AggregateScenario<RiskTreatmentAssertion>(owner);
        var treatmentId = owner.Pending!.TreatmentId;
        var operation = NativeFacts.Root(owner, reviewId, nameof(ReviewRiskControlTreatment), 1);

        // Act
        var result = owner.Review(operation, treatmentId, outcome, "Reviewed independently.",
            operation.Actor, operation.AcceptedAt, null, NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var fact = Assert.IsType<RiskControlTreatmentReviewed>(Assert.Single(scenario.PendingEvents));
        Assert.Equal(outcome, fact.Outcome);
        Assert.Equal(operation.Actor, fact.Reviewer);
        Assert.Null(fact.SeparationOfDutiesWaiverId);
        Assert.Equal(2, owner.Revision);
        Assert.Null(owner.Pending);
        Assert.Equal(outcome == "accept" ? treatmentId : (Uuid?)null, owner.Accepted?.TreatmentId);
    }

    [Theory]
    [InlineData("accept")]
    [InlineData("reject")]
    public void ShouldRecordReviewGivenProposerWithWaiverReference(string outcome)
    {
        // Arrange
        var proposer = Uuid.CreateVersion4();
        var reviewId = Uuid.CreateVersion4();
        var owner = PendingProposal(proposer, reviewId);
        var scenario = new AggregateScenario<RiskTreatmentAssertion>(owner);
        var treatmentId = owner.Pending!.TreatmentId;
        var operation = NativeFacts.Root(owner, reviewId, nameof(ReviewRiskControlTreatment), 1, proposer);
        var waiver = Uuid.CreateVersion4();

        // Act
        var result = owner.Review(operation, treatmentId, outcome, "Reviewed under the named waiver.",
            operation.Actor, operation.AcceptedAt, waiver, NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        var fact = Assert.IsType<RiskControlTreatmentReviewed>(Assert.Single(scenario.PendingEvents));
        Assert.Equal(outcome, fact.Outcome);
        Assert.Equal(operation.Actor, fact.Reviewer);
        Assert.Equal(waiver, fact.SeparationOfDutiesWaiverId);
        Assert.Equal(2, owner.Revision);
        Assert.Null(owner.Pending);
        Assert.Equal(outcome == "accept" ? treatmentId : (Uuid?)null, owner.Accepted?.TreatmentId);
    }

    [Fact]
    public void ShouldRefuseWithoutFactGivenStaleSelfReview()
    {
        // Arrange
        var proposer = Uuid.CreateVersion4();
        var reviewId = Uuid.CreateVersion4();
        var owner = PendingProposal(proposer, reviewId);
        var scenario = new AggregateScenario<RiskTreatmentAssertion>(owner);
        var treatmentId = owner.Pending!.TreatmentId;
        var operation = NativeFacts.Root(owner, reviewId, nameof(ReviewRiskControlTreatment), 0, proposer);

        // Act
        var result = owner.Review(operation, treatmentId, "accept", "Stale review.",
            operation.Actor, operation.AcceptedAt, null, NativeFacts.Ref(), NativeFacts.Ref());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, result.Error!.Kind);
        Assert.Empty(scenario.PendingEvents);
        Assert.Equal(1, owner.Revision);
        Assert.Equal(treatmentId, owner.Pending!.TreatmentId);
    }

    static RiskTreatmentAssertion PendingProposal(Uuid proposer, Uuid reviewId)
    {
        var proposalId = Uuid.CreateVersion4();
        var seed = new RiskTreatmentAssertion(Uuid.CreateVersion4(), Uuid.CreateVersion4(),
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), proposalId);
        var operation = NativeFacts.Root(seed, proposalId, nameof(ProposeRiskControlTreatment), 0, proposer);
        Assert.True(seed.Propose(operation, NativeFacts.Ref(RiskContent.TreatmentSchema), proposer,
            NativeFacts.Ref(), NativeFacts.Ref()).IsSuccess);
        return NativeFacts.Replay(new RiskTreatmentAssertion(seed.TenantId, seed.ProgramId, seed.RiskId,
            seed.ControlId, reviewId), NativeFacts.Raised(seed));
    }
}
