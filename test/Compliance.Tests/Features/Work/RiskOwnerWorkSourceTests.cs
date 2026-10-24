using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Operations;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Features.Operations;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Work;

public sealed class RiskOwnerWorkSourceTests
{
    [Fact]
    public async Task ShouldOfferCompletionReviewGivenSubmittedActionOwner()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var risk = Uuid.CreateVersion4();
        var action = Uuid.CreateVersion4();
        var submission = Uuid.CreateVersion4();
        await AddAsync(fixture, risk, action);
        var open = Assert.Single(await CandidatesAsync(fixture));

        // Act
        await SubmitAsync(fixture, risk, action, submission);
        var review = Assert.Single(await CandidatesAsync(fixture));

        // Assert
        Assert.Equal(WorkSource.RiskTreatmentAction, open.Kind);
        Assert.Equal("complete", open.NextAction);
        Assert.Equal(new OperatingHolder(OperatingAuthority.MemberHolder, fixture.OwnerMemberId), open.Responsible);
        Assert.Equal(WorkSource.RiskTreatmentActionReview, review.Kind);
        Assert.Equal(submission, review.SourceId);
        Assert.Equal("review", review.NextAction);
        Assert.Equal(new HashSet<Uuid> { fixture.OwnerMemberId, fixture.LeadMemberId }, review.Excluded);
        Assert.EndsWith($"/risks/{risk}/treatment-actions/{action}/completion-reviews", review.ActionPath,
            StringComparison.Ordinal);
        Assert.Equal(2, (await OwnerAsync(fixture, risk, action)).Revision);
    }

    [Fact]
    public async Task ShouldRestoreActionAfterRejectedReviewGivenAcceptedAndRejectedReviews()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var risk = Uuid.CreateVersion4();
        var action = Uuid.CreateVersion4();
        await AddAsync(fixture, risk, action);
        await SubmitAsync(fixture, risk, action, Uuid.CreateVersion4());

        // Act
        await RiskSeeds.ReviewActionAsync(fixture.Provider, fixture.TenantId, fixture.ProgramId, risk, action,
            "reject", "Wrong tenant.", Actor(fixture), DateTimeOffset.UtcNow);
        var reopened = Assert.Single(await CandidatesAsync(fixture));
        await SubmitAsync(fixture, risk, action, Uuid.CreateVersion4());
        await RiskSeeds.ReviewActionAsync(fixture.Provider, fixture.TenantId, fixture.ProgramId, risk, action,
            "accept", "Evidence matches.", Actor(fixture), DateTimeOffset.UtcNow);

        // Assert
        Assert.Equal(WorkSource.RiskTreatmentAction, reopened.Kind);
        Assert.Empty(await CandidatesAsync(fixture));
        Assert.Equal("completed", (await OwnerAsync(fixture, risk, action)).Status);
    }

    [Theory]
    [InlineData("accept")]
    [InlineData("reject")]
    public async Task ShouldRemovePendingControlTreatmentReviewGivenIndependentDecision(string outcome)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var risk = Uuid.CreateVersion4();
        var treatment = Uuid.CreateVersion4();
        await RiskSeeds.ProposeTreatmentAsync(fixture.Provider, fixture.TenantId, fixture.ProgramId, risk,
            treatment, fixture.ControlId, fixture.ControlVersionId, "Addresses the risk.", fixture.LeadMemberId,
            Actor(fixture), DateTimeOffset.UtcNow);
        var pending = Assert.Single(await CandidatesAsync(fixture));

        // Act
        await RiskSeeds.ReviewTreatmentAsync(fixture.Provider, fixture.TenantId, fixture.ProgramId, risk,
            fixture.ControlId, treatment, outcome, "Independently reviewed.",
            ActorReference.ForMember(fixture.ApproverMemberId, "Approver"), DateTimeOffset.UtcNow);

        // Assert
        Assert.Equal(WorkSource.RiskControlTreatmentReview, pending.Kind);
        Assert.Equal(treatment, pending.SourceId);
        Assert.Contains(fixture.LeadMemberId, pending.Excluded);
        Assert.Empty(await CandidatesAsync(fixture));
    }

    [Fact]
    public async Task ShouldRemoveSubmittedCompletionReviewGivenActionCancellation()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var risk = Uuid.CreateVersion4();
        var action = Uuid.CreateVersion4();
        await AddAsync(fixture, risk, action);
        await SubmitAsync(fixture, risk, action, Uuid.CreateVersion4());
        Assert.Equal(WorkSource.RiskTreatmentActionReview, Assert.Single(await CandidatesAsync(fixture)).Kind);

        // Act
        await RiskSeeds.CancelActionAsync(fixture.Provider, fixture.TenantId, fixture.ProgramId, risk, action,
            "Work no longer required.", Actor(fixture), DateTimeOffset.UtcNow);

        // Assert
        Assert.Empty(await CandidatesAsync(fixture));
        Assert.Equal("cancelled", (await OwnerAsync(fixture, risk, action)).Status);
    }

    [Fact]
    public async Task ShouldTrackOwnerRevisionsIndependentlyGivenInterleavedActions()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var risk1 = Uuid.CreateVersion4();
        var risk2 = Uuid.CreateVersion4();
        var action1 = Uuid.CreateVersion4();
        var action2 = Uuid.CreateVersion4();
        await AddAsync(fixture, risk1, action1);
        await AddAsync(fixture, risk2, action2);

        // Act
        await SubmitAsync(fixture, risk1, action1, Uuid.CreateVersion4());
        var work = await CandidatesAsync(fixture);

        // Assert
        Assert.Equal(2, work.Count);
        Assert.Equal(2, (await OwnerAsync(fixture, risk1, action1)).Revision);
        Assert.Equal(1, (await OwnerAsync(fixture, risk2, action2)).Revision);
        Assert.Contains(work, item => item.Kind == WorkSource.RiskTreatmentAction && item.SourceId == action2);
        Assert.Single(work, item => item.Kind == WorkSource.RiskTreatmentActionReview);
    }

    [Fact]
    public async Task ShouldKeepRiskActionWorkDistinctGivenSameActionAndSubmissionIdsInDifferentRisks()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var first = Uuid.CreateVersion4();
        var second = Uuid.CreateVersion4();
        var action = Uuid.CreateVersion4();
        var submission = Uuid.CreateVersion4();
        await AddAsync(fixture, first, action);
        await AddAsync(fixture, second, action);
        var open = await CandidatesAsync(fixture);

        // Act
        await SubmitAsync(fixture, first, action, submission);
        await SubmitAsync(fixture, second, action, submission);
        var reviews = await CandidatesAsync(fixture);

        // Assert
        Assert.Equal(2, open.Select(item => item.WorkItemId).Distinct().Count());
        Assert.Equal(2, reviews.Select(item => item.WorkItemId).Distinct().Count());
        Assert.All(reviews, item => Assert.Equal(WorkSource.RiskTreatmentActionReview, item.Kind));
        Assert.Contains(reviews, item => item.ActionPath.Contains(first.ToString(), StringComparison.Ordinal));
        Assert.Contains(reviews, item => item.ActionPath.Contains(second.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task ShouldKeepRiskWorkTenantScopedGivenSharedEventStore()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var other = Uuid.CreateVersion4();
        var risk = Uuid.CreateVersion4();
        var action = Uuid.CreateVersion4();
        await AddAsync(fixture, risk, action);
        await RiskSeeds.AddActionAsync(fixture.Provider, other, fixture.ProgramId, risk, action, "Foreign action",
            "Foreign target", "Foreign evidence", fixture.Today.AddDays(9), Uuid.CreateVersion4(), [], Actor(fixture),
            DateTimeOffset.UtcNow);

        // Act
        var own = Assert.Single(await CandidatesAsync(fixture));
        var foreign = Assert.Single(await CandidatesAsync(fixture, other));

        // Assert
        Assert.Equal("Enforce MFA", own.Summary);
        Assert.Equal("Foreign action", foreign.Summary);
        Assert.Contains(fixture.TenantId.ToString(), own.ActionPath, StringComparison.Ordinal);
        Assert.Contains(other.ToString(), foreign.ActionPath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldRejectSkippedActionRevisionGivenBrokenOwnerHistory()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var risk = Uuid.CreateVersion4();
        var action = Uuid.CreateVersion4();
        await AddAsync(fixture, risk, action);
        var added = (await OwnerAsync(fixture, risk, action)).Added! with { Revision = 2 };
        var replay = new AggregateScenario<RiskTreatmentAction>(new RiskTreatmentAction(fixture.TenantId,
            fixture.ProgramId, risk, action));

        // Act
        var failure = Record.Exception(() => replay.Given(added));

        // Assert
        Assert.IsType<InvalidOperationException>(failure);
    }

    static ActorReference Actor(OperationsFixture fixture) => ActorReference.ForMember(fixture.LeadMemberId, "Lead");

    static Task AddAsync(OperationsFixture fixture, Uuid risk, Uuid action) => RiskSeeds.AddActionAsync(fixture.Provider,
        fixture.TenantId, fixture.ProgramId, risk, action, "Enforce MFA", "MFA is required", "IdP export",
        fixture.Today.AddDays(9), fixture.OwnerMemberId, [], Actor(fixture), DateTimeOffset.UtcNow);

    static Task SubmitAsync(OperationsFixture fixture, Uuid risk, Uuid action, Uuid submission) => RiskSeeds.SubmitActionAsync(
        fixture.Provider, fixture.TenantId, fixture.ProgramId, risk, action, submission, "MFA enforced", [Uuid.CreateVersion4()],
        fixture.LeadMemberId, Actor(fixture), DateTimeOffset.UtcNow);

    static Task<RiskTreatmentAction> OwnerAsync(OperationsFixture fixture, Uuid risk, Uuid action) =>
        ProgramManagementServices.HydrateAsync(fixture.Provider,
            new RiskTreatmentAction(fixture.TenantId, fixture.ProgramId, risk, action));

    static async Task<IReadOnlyList<WorkCandidate>> CandidatesAsync(OperationsFixture fixture, Uuid? tenant = null)
    {
        await using var scope = fixture.Provider.CreateAsyncScope();
        var projected = new HashSet<string>
        {
            WorkSource.ControlOccurrence, WorkSource.OccurrenceReview, WorkSource.CorrectiveAction,
            WorkSource.EvidenceRequest, WorkSource.ControlEvaluationReview, WorkSource.ControlOperatingPlanApproval,
            WorkSource.ControlCriterionMappingReview, WorkSource.CriterionApplicabilityReview,
            WorkSource.AccessReviewReview, WorkSource.AccessReviewRemediation, WorkSource.AdvisorFeedback,
            FindingClosureWork.Kind,
        };
        var result = await WorkSource.LoadAsync(scope.ServiceProvider.GetRequiredService<IAggregateReader>(), scope.ServiceProvider.GetRequiredService<ControlReader>(), null,
            scope.ServiceProvider.GetRequiredService<RiskGovernanceReader>(), null, null, null, null, null,
            tenant ?? fixture.TenantId, fixture.ProgramId, fixture.Today, fixture.Today.AddDays(90), DateTimeOffset.UtcNow,
            null, null, projected, CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value;
    }
}
