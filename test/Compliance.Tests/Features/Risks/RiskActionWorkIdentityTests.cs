using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Controls;
using Bdgrz.Compliance.Features.Risks;
using Bdgrz.Compliance.Features.Work;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.Risks;

public sealed class RiskActionWorkIdentityTests
{
    [Fact]
    public async Task ShouldKeepReviewAssignmentOnExactActionGivenSameSubmissionIdentityOnAnotherAction()
    {
        // Arrange
        var fixture = await RiskGovernanceHandlerTests.Fixture.CreateAsync("mitigate");
        await using var provider = fixture.Provider;
        var accountable = await fixture.SeedMemberAsync();
        var reviewer = await fixture.SeedMemberAsync();
        var evidence = await fixture.SeedEvidenceAsync(true, accountable);
        var first = fixture.AddAction(accountable, [evidence], 0) with { Title = "Action A" };
        var second = fixture.AddAction(accountable, [evidence], 0) with { Title = "Action B" };
        await fixture.Scenario(fixture.AssessorUserId).When(first).ExpectSuccess();
        await fixture.Scenario(fixture.AssessorUserId).When(second).ExpectSuccess();
        var submission = Uuid.CreateVersion4();
        await PersonalRiskSignoffTransportTests.HttpAsync(provider, fixture.AssessorUserId,
            fixture.Submit(first.ActionId, 1, [evidence]) with { CommandId = submission });
        var queue = await fixture.Scenario(fixture.AssessorUserId).When(new ListWork(fixture.TenantId, fixture.ProgramId, "all")).ExpectSuccess();
        var item = Assert.Single(queue.Value.Items, work => work.Kind == WorkSource.RiskTreatmentActionReview && work.SourceId == submission);
        await fixture.Scenario(fixture.AssessorUserId).When(new AssignWorkItem(fixture.TenantId, fixture.ProgramId,
            item.WorkItemId, item.AssignmentRevision, reviewer.MemberId, "Review Action A only.")).ExpectSuccess();
        await PersonalRiskSignoffTransportTests.HttpAsync(provider, fixture.AssessorUserId,
            fixture.Submit(second.ActionId, 1, [evidence]) with { CommandId = submission });

        // Act
        var refused = await PersonalRiskSignoffTransportTests.HttpAsync(provider, reviewer.UserId,
            fixture.ReviewAction(second.ActionId, 2, "reject"), RequestErrorKind.Forbidden);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, refused.Error?.Kind);
        await using var scope = provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var untouched = await reader.HydrateAsync(new RiskTreatmentAction(fixture.TenantId, fixture.ProgramId,
            fixture.RiskId, second.ActionId));
        Assert.Equal(2, untouched.Revision);
        Assert.Null(untouched.PendingReview);
    }

    [Fact]
    public async Task ShouldKeepQueueIdentityAndSubmitterDutiesDistinctGivenSameSubmissionOnTwoActions()
    {
        // Arrange
        var fixture = await RiskGovernanceHandlerTests.Fixture.CreateAsync("mitigate");
        await using var provider = fixture.Provider;
        var accountable = await fixture.SeedMemberAsync();
        var evidence = await fixture.SeedEvidenceAsync(true, accountable);
        var first = fixture.AddAction(accountable, [evidence], 0) with { Title = "Action A" };
        var second = fixture.AddAction(accountable, [evidence], 0) with { Title = "Action B" };
        await fixture.Scenario(fixture.AssessorUserId).When(first).ExpectSuccess();
        await fixture.Scenario(fixture.AssessorUserId).When(second).ExpectSuccess();
        var submission = Uuid.CreateVersion4();
        await PersonalRiskSignoffTransportTests.HttpAsync(provider, fixture.AssessorUserId,
            fixture.Submit(first.ActionId, 1, [evidence]) with { CommandId = submission });
        await PersonalRiskSignoffTransportTests.HttpAsync(provider, fixture.ApproverUserId,
            fixture.Submit(second.ActionId, 1, [evidence]) with { CommandId = submission });

        // Act
        var candidates = await CandidatesAsync(fixture);
        var reviews = candidates.Where(work => work.Kind == WorkSource.RiskTreatmentActionReview).ToArray();
        var queue = await fixture.Scenario(fixture.AssessorUserId).When(new ListWork(fixture.TenantId, fixture.ProgramId, "all")).ExpectSuccess();

        // Assert
        Assert.Equal(2, reviews.Length);
        Assert.Equal(2, reviews.Select(work => work.WorkItemId).Distinct().Count());
        var firstReview = Assert.Single(reviews, work => work.ActionPath.Contains(first.ActionId.ToString(), StringComparison.Ordinal));
        var secondReview = Assert.Single(reviews, work => work.ActionPath.Contains(second.ActionId.ToString(), StringComparison.Ordinal));
        Assert.Contains(RbacIds.Member(fixture.TenantId, fixture.AssessorUserId), firstReview.Excluded);
        Assert.DoesNotContain(RbacIds.Member(fixture.TenantId, fixture.ApproverUserId), firstReview.Excluded);
        Assert.Contains(RbacIds.Member(fixture.TenantId, fixture.ApproverUserId), secondReview.Excluded);
        Assert.DoesNotContain(RbacIds.Member(fixture.TenantId, fixture.AssessorUserId), secondReview.Excluded);
        Assert.Equal(2, queue.Value.Items.Count(work => work.Kind == WorkSource.RiskTreatmentActionReview && work.SourceId == submission));
    }

    [Fact]
    public async Task ShouldRequireNewScopedAssignmentGivenOnlyOldAmbiguousReviewWorkIdentity()
    {
        // Arrange
        var fixture = await RiskGovernanceHandlerTests.Fixture.CreateAsync("mitigate");
        await using var provider = fixture.Provider;
        var accountable = await fixture.SeedMemberAsync();
        var reviewer = await fixture.SeedMemberAsync();
        var evidence = await fixture.SeedEvidenceAsync(true, accountable);
        var action = fixture.AddAction(accountable, [evidence], 0);
        await fixture.Scenario(fixture.AssessorUserId).When(action).ExpectSuccess();
        var submission = Uuid.CreateVersion4();
        await PersonalRiskSignoffTransportTests.HttpAsync(provider, fixture.AssessorUserId,
            fixture.Submit(action.ActionId, 1, [evidence]) with { CommandId = submission });
        var oldId = WorkSource.RiskTreatmentWorkItemId(fixture.ProgramId, fixture.RiskId, submission, WorkSource.RiskTreatmentActionReview);
        await ProgramManagementServices.SeedAsync(provider, new WorkItemAssignment(fixture.TenantId, fixture.ProgramId, oldId), assignment =>
        {
            Assert.Null(assignment.AssignTo(0, WorkItemAssignment.Assign, reviewer.MemberId, null, "Old unscoped assignment.",
                ActorReference.ForMember(RbacIds.Member(fixture.TenantId, fixture.AssessorUserId), "Lead"), DateTimeOffset.UtcNow));
            return Result.Success;
        });

        // Act
        var refused = await PersonalRiskSignoffTransportTests.HttpAsync(provider, reviewer.UserId,
            fixture.ReviewAction(action.ActionId, 2, "reject"), RequestErrorKind.Forbidden);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, refused.Error?.Kind);
    }

    static async Task<IReadOnlyList<WorkCandidate>> CandidatesAsync(RiskGovernanceHandlerTests.Fixture fixture)
    {
        await using var scope = fixture.Provider.CreateAsyncScope();
        var projected = new HashSet<string>
        {
            WorkSource.ControlOccurrence, WorkSource.OccurrenceReview, WorkSource.CorrectiveAction, WorkSource.EvidenceRequest,
            WorkSource.ControlEvaluationReview, WorkSource.ControlOperatingPlanApproval, WorkSource.ControlCriterionMappingReview,
            WorkSource.CriterionApplicabilityReview, WorkSource.AccessReviewReview, WorkSource.AccessReviewRemediation,
            WorkSource.AdvisorFeedback, FindingClosureWork.Kind,
        };
        var result = await WorkSource.LoadAsync(scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
            scope.ServiceProvider.GetRequiredService<ControlReader>(), null, scope.ServiceProvider.GetRequiredService<RiskGovernanceReader>(),
            null, null, null, null, null, fixture.TenantId, fixture.ProgramId, DateOnly.FromDateTime(DateTime.UtcNow),
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(90), DateTimeOffset.UtcNow, null, null, projected, CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value;
    }
}
