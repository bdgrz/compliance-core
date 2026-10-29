using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Services;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.Independence.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Events;
using Bdgrz.Compliance.Features.Work.Queries;
namespace Bdgrz.Compliance.Tests.Features.Risks.Tests;

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
        await fixture.Scenario(fixture.AssessorUserId).When(new AssignWorkItem(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId,
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
        var oldOwner = new WorkItemAssignment(fixture.TenantId, fixture.ProgramId, oldId);
        var oldAssignment = DomainEventSeed.Attach(new WorkItemAssigned(fixture.TenantId, fixture.ProgramId, oldId,
            1, WorkItemAssignment.Assign, reviewer.MemberId, null, "Old unscoped assignment.",
            ActorReference.ForMember(RbacIds.Member(fixture.TenantId, fixture.AssessorUserId), "Lead"), DateTimeOffset.UtcNow),
            oldOwner.Id, 1);
        await provider.GetRequiredService<IEventStore>().AppendAsync(oldOwner.Stream, 0, [oldAssignment]);

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
