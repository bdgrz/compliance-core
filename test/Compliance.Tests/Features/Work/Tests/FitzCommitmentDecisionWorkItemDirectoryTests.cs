using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Commitments.Contracts;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Readers;
using Bdgrz.Compliance.Features.Work.Services;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPopulations.Tests;
using Bdgrz.Compliance.Tests.Features.AuditorRequests.Tests;
using Bdgrz.Compliance.Tests.Features.Boundaries.Tests;
using Bdgrz.Compliance.Tests.Features.Changes.Tests;
using Bdgrz.Compliance.Tests.Features.Commitments.Tests;
using Bdgrz.Compliance.Tests.Features.ControlMappings.Tests;
using Bdgrz.Compliance.Tests.Features.Controls.Tests;
using Bdgrz.Compliance.Tests.Features.Criteria.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.Exports.Tests;
using Bdgrz.Compliance.Tests.Features.Incidents.Tests;
using Bdgrz.Compliance.Tests.Features.MethodologyTemplates.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Programs.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Retention.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.Snapshots.Tests;
using Bdgrz.Compliance.Tests.Features.SourceFacts.Tests;
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.TechnologyInventory.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.Commitments.Aggregates;
using Bdgrz.Compliance.Features.Commitments.Events;
using Bdgrz.Compliance.Features.Commitments.Models;
using Bdgrz.Compliance.Features.Commitments.Readers;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Responsibilities.Commands;
using Bdgrz.Compliance.Features.Responsibilities.Events;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Work.Tests;

public sealed class FitzCommitmentDecisionWorkItemDirectoryTests
{
    const string SourceReference = "MSA 4.1";

    [Fact]
    public async Task ShouldProjectReviewApprovalAndCompletionGivenCommitmentDecisionLifecycle()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var draftId = CommitmentDraft.IdFor(fixture.TenantId, fixture.ProgramId, "service_commitment", "SC-WORK");
        var created = await SeedDraftAsync(fixture, draftId);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = new FitzCommitmentDecisionWorkItemDirectory(new InMemoryKvClient(),
            sourceScope.ServiceProvider.GetRequiredService<CommitmentReader>());
        await ProjectAsync(directory, events, fixture.TenantId, created);
        var responsibilityScope = new ResponsibilityScope("commitment", draftId, draftId, 1);
        var reviewerAssignmentId = Uuid.CreateVersion4();
        var approverAssignmentId = Uuid.CreateVersion4();
        var assignedAt = created.ChangedAt.AddSeconds(1);
        await ApplyDraftAsync(fixture, draftId, draft =>
        {
            Assert.Null(draft.AssignResponsibility(responsibilityScope, reviewerAssignmentId,
                fixture.ReviewerMemberId, ResponsibilityType.AssignedReviewer,
                fixture.LeadMemberId, "Lead", assignedAt, created.ChangedAt, null, []));
            return Result.Success;
        });
        await ProjectAsync(directory, events, fixture.TenantId, new ResponsibilityAssigned(
            fixture.TenantId, reviewerAssignmentId, fixture.ReviewerMemberId,
            ResponsibilityType.AssignedReviewer, responsibilityScope, assignedAt,
            fixture.LeadMemberId, created.ChangedAt, null, [], "Lead"));
        await ApplyDraftAsync(fixture, draftId, draft =>
        {
            Assert.Null(draft.AssignResponsibility(responsibilityScope, approverAssignmentId,
                fixture.ApproverMemberId, ResponsibilityType.PolicyApprover,
                fixture.LeadMemberId, "Lead", assignedAt, created.ChangedAt, null, []));
            return Result.Success;
        });
        await ProjectAsync(directory, events, fixture.TenantId, new ResponsibilityAssigned(
            fixture.TenantId, approverAssignmentId, fixture.ApproverMemberId,
            ResponsibilityType.PolicyApprover, responsibilityScope, assignedAt,
            fixture.LeadMemberId, created.ChangedAt, null, [], "Lead"));

        var initial = Assert.Single((await directory.LoadProgramAsync(fixture.TenantId,
            fixture.ProgramId, created.ChangedAt, DateOnly.MaxValue, null)).Value);
        var reviewId = Uuid.CreateVersion4();
        var reviewedAt = created.ChangedAt.AddMinutes(1);
        await ApplyDraftAsync(fixture, draftId, draft =>
        {
            Assert.Null(draft.Review(fixture.ProgramId, draft.Revision, reviewId, "accept",
                "Security lead", "applicable", "supported", null, "Verified against source",
                fixture.ReviewerMemberId, "Reviewer", reviewedAt, null, SourceReference,
                "Signed MSA section 4.1"));
            return Result.Success;
        });

        // Act
        await ProjectAsync(directory, events, fixture.TenantId, new CommitmentReviewedContent(
            fixture.TenantId, fixture.ProgramId, draftId, 1, reviewId, "accept",
            "Security lead", "applicable", "supported", null, "Verified against source",
            null, null, null, fixture.ReviewerMemberId, "Reviewer", reviewedAt));
        var approval = Assert.Single((await directory.LoadProgramAsync(fixture.TenantId,
            fixture.ProgramId, reviewedAt, DateOnly.MaxValue, null)).Value);
        var approvalId = Uuid.CreateVersion4();
        var approvedAt = reviewedAt.AddMinutes(1);
        await ApplyDraftAsync(fixture, draftId, draft =>
        {
            Assert.Null(draft.Approve(fixture.ProgramId, draft.Revision, approvalId, reviewId,
                fixture.Today, "Approved", "impact-digest", fixture.ApproverMemberId,
                "Approver", approvedAt));
            return Result.Success;
        });
        await ProjectAsync(directory, events, fixture.TenantId, new CommitmentApprovedContent(
            fixture.TenantId, fixture.ProgramId, draftId, 1, approvalId, reviewId, 1,
            fixture.Today, "impact-digest", "Approved", fixture.ApproverMemberId,
            "Approver", approvedAt));
        var completed = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            approvedAt, DateOnly.MaxValue, null);

        // Assert
        Assert.Equal("commitment_draft_review", initial.Kind);
        Assert.Equal(fixture.ReviewerMemberId, initial.Responsible.Id);
        Assert.Equal(draftId, initial.SourceId);
        Assert.Equal($"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/" +
                     $"commitment-drafts/{draftId}/reviews", initial.ActionPath);
        Assert.Equal("commitment_draft_approval", approval.Kind);
        Assert.Equal(fixture.ApproverMemberId, approval.Responsible.Id);
        Assert.NotEqual(initial.WorkItemId, approval.WorkItemId);
        Assert.Equal($"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/" +
                     $"commitment-drafts/{draftId}/approvals", approval.ActionPath);
        Assert.Empty(completed.Value);
        Assert.Equal(5, await directory.LoadRevisionAsync(fixture.TenantId));
        Assert.Equal(await ReadCommitmentCheckpointAsync(events, fixture.TenantId),
            await directory.LoadCheckpointAsync(fixture.TenantId));
        Assert.Empty((await directory.LoadProgramAsync(fixture.TenantId,
            Uuid.CreateVersion4(), approvedAt, DateOnly.MaxValue, null)).Value);
        Assert.Empty((await directory.LoadProgramAsync(Uuid.CreateVersion4(), fixture.ProgramId,
            approvedAt, DateOnly.MaxValue, null)).Value);
    }

    [Fact]
    public async Task ShouldReopenReviewWithNewIdentityGivenRequestedChangesAndRevisedDraft()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var draftId = CommitmentDraft.IdFor(fixture.TenantId, fixture.ProgramId, "service_commitment", "SC-WORK");
        var created = await SeedDraftAsync(fixture, draftId);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = new FitzCommitmentDecisionWorkItemDirectory(new InMemoryKvClient(),
            sourceScope.ServiceProvider.GetRequiredService<CommitmentReader>());
        await ProjectAsync(directory, events, fixture.TenantId, created);
        var initial = Assert.Single((await directory.LoadProgramAsync(fixture.TenantId,
            fixture.ProgramId, created.ChangedAt, DateOnly.MaxValue, null)).Value);
        var reviewId = Uuid.CreateVersion4();
        var reviewedAt = created.ChangedAt.AddMinutes(1);
        await ApplyDraftAsync(fixture, draftId, draft =>
        {
            Assert.Null(draft.Review(fixture.ProgramId, draft.Revision, reviewId,
                "request_changes", null, null, null, null, "Clarify the source mapping",
                fixture.ReviewerMemberId, "Reviewer", reviewedAt));
            return Result.Success;
        });

        // Act
        await ProjectAsync(directory, events, fixture.TenantId, new CommitmentReviewedContent(
            fixture.TenantId, fixture.ProgramId, draftId, 1, reviewId, "request_changes",
            null, null, null, null, "Clarify the source mapping", null, null, null,
            fixture.ReviewerMemberId, "Reviewer", reviewedAt));
        var afterChanges = Assert.Single((await directory.LoadProgramAsync(fixture.TenantId,
            fixture.ProgramId, reviewedAt, DateOnly.MaxValue, null)).Value);
        var revisedAt = reviewedAt.AddMinutes(1);
        await ApplyDraftAsync(fixture, draftId, draft =>
        {
            Assert.Null(draft.Revise(fixture.ProgramId, draft.Revision,
                "Updated commitment statement", "Updated context", SourceReference,
                fixture.LeadMemberId, "Lead", revisedAt));
            return Result.Success;
        });
        await ProjectAsync(directory, events, fixture.TenantId, new CommitmentDraftRevisedContent(
            fixture.TenantId, fixture.ProgramId, draftId, 2,
            "Updated commitment statement", "Updated context", SourceReference,
            fixture.LeadMemberId, "Lead", revisedAt));
        var revised = Assert.Single((await directory.LoadProgramAsync(fixture.TenantId,
            fixture.ProgramId, revisedAt, DateOnly.MaxValue, null)).Value);

        // Assert
        Assert.Equal(initial.WorkItemId, afterChanges.WorkItemId);
        Assert.Equal("commitment_draft_review", revised.Kind);
        Assert.NotEqual(initial.WorkItemId, revised.WorkItemId);
        Assert.Contains("revision 2", revised.Reason, StringComparison.Ordinal);
        Assert.Equal(revisedAt, revised.CreatedAt);
    }

    [Fact]
    public async Task ShouldApplyResponsibilityStartAndRecordedRevocationGivenCommitmentAssignment()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var draftId = CommitmentDraft.IdFor(fixture.TenantId, fixture.ProgramId, "service_commitment", "SC-WORK");
        var created = await SeedDraftAsync(fixture, draftId);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = new FitzCommitmentDecisionWorkItemDirectory(new InMemoryKvClient(),
            sourceScope.ServiceProvider.GetRequiredService<CommitmentReader>());
        await ProjectAsync(directory, events, fixture.TenantId, created);
        var assignmentId = Uuid.CreateVersion4();
        var now = created.ChangedAt.AddHours(2);
        var effectiveFrom = now.AddHours(1);
        var revokedAt = effectiveFrom.AddHours(2);
        var scope = new ResponsibilityScope("commitment", draftId, draftId, 1);
        await ApplyDraftAsync(fixture, draftId, draft =>
        {
            Assert.Null(draft.AssignResponsibility(scope, assignmentId,
                fixture.ReviewerMemberId, ResponsibilityType.AssignedReviewer,
                fixture.LeadMemberId, "Lead", now, effectiveFrom, null, []));
            return Result.Success;
        });
        await ProjectAsync(directory, events, fixture.TenantId, new ResponsibilityAssigned(
            fixture.TenantId, assignmentId, fixture.ReviewerMemberId,
            ResponsibilityType.AssignedReviewer, scope, now, fixture.LeadMemberId,
            effectiveFrom, null, [], "Lead"));

        // Act
        var beforeStart = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            effectiveFrom.AddTicks(-1), DateOnly.MaxValue, null);
        var atStart = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            effectiveFrom, DateOnly.MaxValue, null);
        await ApplyDraftAsync(fixture, draftId, draft =>
        {
            Assert.Null(draft.RevokeResponsibility(scope, assignmentId,
                fixture.LeadMemberId, "Lead", revokedAt, "Responsibility ended."));
            return Result.Success;
        });
        await ProjectAsync(directory, events, fixture.TenantId, new ResponsibilityRevoked(
            fixture.TenantId, assignmentId, scope, fixture.LeadMemberId, revokedAt,
            "Responsibility ended.", "Lead"));
        var immediatelyAfterRevocationWasRecorded = await directory.LoadProgramAsync(
            fixture.TenantId, fixture.ProgramId, revokedAt.AddTicks(-1), DateOnly.MaxValue,
            null);
        var atRevocation = await directory.LoadProgramAsync(fixture.TenantId,
            fixture.ProgramId, revokedAt, DateOnly.MaxValue, null);

        // Assert
        Assert.Equal(OperatingAuthority.ProgramManagerHolder,
            Assert.Single(beforeStart.Value).Responsible.Kind);
        Assert.Equal(fixture.ReviewerMemberId, Assert.Single(atStart.Value).Responsible.Id);
        Assert.Equal(OperatingAuthority.ProgramManagerHolder,
            Assert.Single(immediatelyAfterRevocationWasRecorded.Value).Responsible.Kind);
        Assert.Equal(OperatingAuthority.ProgramManagerHolder,
            Assert.Single(atRevocation.Value).Responsible.Kind);
    }

    [Fact]
    public async Task ShouldApplyResponsibilityEndAtReadTimeGivenProjectedAssignment()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var draftId = CommitmentDraft.IdFor(fixture.TenantId, fixture.ProgramId, "service_commitment", "SC-WORK");
        var created = await SeedDraftAsync(fixture, draftId);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = new FitzCommitmentDecisionWorkItemDirectory(new InMemoryKvClient(),
            sourceScope.ServiceProvider.GetRequiredService<CommitmentReader>());
        await ProjectAsync(directory, events, fixture.TenantId, created);
        var assignmentId = Uuid.CreateVersion4();
        var assignedAt = created.ChangedAt.AddHours(1);
        var effectiveFrom = assignedAt.AddMinutes(1);
        var effectiveUntil = effectiveFrom.AddHours(1);
        var scope = new ResponsibilityScope("commitment", draftId, draftId, 1);
        await ApplyDraftAsync(fixture, draftId, draft =>
        {
            Assert.Null(draft.AssignResponsibility(scope, assignmentId,
                fixture.ReviewerMemberId, ResponsibilityType.AssignedReviewer,
                fixture.LeadMemberId, "Lead", assignedAt, effectiveFrom, effectiveUntil, []));
            return Result.Success;
        });
        await ProjectAsync(directory, events, fixture.TenantId, new ResponsibilityAssigned(
            fixture.TenantId, assignmentId, fixture.ReviewerMemberId,
            ResponsibilityType.AssignedReviewer, scope, assignedAt, fixture.LeadMemberId,
            effectiveFrom, effectiveUntil, [], "Lead"));

        // Act
        var beforeStart = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            effectiveFrom.AddTicks(-1), DateOnly.MaxValue, null);
        var active = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            effectiveFrom, DateOnly.MaxValue, null);
        var atEnd = await directory.LoadProgramAsync(fixture.TenantId, fixture.ProgramId,
            effectiveUntil, DateOnly.MaxValue, null);

        // Assert
        Assert.Equal(OperatingAuthority.ProgramManagerHolder,
            Assert.Single(beforeStart.Value).Responsible.Kind);
        Assert.Equal(fixture.ReviewerMemberId, Assert.Single(active.Value).Responsible.Id);
        Assert.Equal(OperatingAuthority.ProgramManagerHolder,
            Assert.Single(atEnd.Value).Responsible.Kind);
    }

    [Fact]
    public async Task ShouldRejectLaggingCheckpointAndAcceptCaughtUpProjectionGivenCommitmentDraft()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var draftId = CommitmentDraft.IdFor(fixture.TenantId, fixture.ProgramId, "service_commitment", "SC-WORK");
        var created = await SeedDraftAsync(fixture, draftId);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var directory = new FitzCommitmentDecisionWorkItemDirectory(new InMemoryKvClient(),
            sourceScope.ServiceProvider.GetRequiredService<CommitmentReader>());
        var consistency = new WorkQueueReadConsistency(events, [directory]);

        // Act
        var behind = await consistency.CaptureAsync(fixture.TenantId, CancellationToken.None);
        await ProjectAsync(directory, events, fixture.TenantId, created);
        var caughtUp = await consistency.CaptureAsync(fixture.TenantId, CancellationToken.None);
        var confirmed = caughtUp.IsSuccess
            ? await consistency.ConfirmUnchangedAndCaughtUpAsync(fixture.TenantId,
                caughtUp.Value, CancellationToken.None)
            : Result.Failure(caughtUp.Error);

        // Assert
        Assert.False(behind.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, behind.Error!.Kind);
        Assert.True(behind.Error.IsTransient);
        Assert.True(caughtUp.IsSuccess);
        Assert.True(confirmed.IsSuccess);
    }

    static async Task<CommitmentDraftCreatedContent> SeedDraftAsync(OperationsFixture fixture,
        Uuid draftId)
    {
        var createdAt = At(fixture.Today).AddMinutes(-1);
        var created = new CommitmentDraftCreatedContent(fixture.TenantId, fixture.ProgramId, draftId,
            Uuid.CreateVersion4(), Uuid.CreateVersion4(), "service_commitment", "SC-WORK",
            "The service protects customer data.", "Security commitment.", SourceReference,
            fixture.LeadMemberId, "Lead", createdAt);
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new CommitmentRules(fixture.TenantId, draftId), draft =>
            {
                Assert.True(draft.Create(created.ProgramId, created.CreateRequestId,
                    created.ServiceId, created.Kind, created.Identifier, created.Statement,
                    created.Context, created.SourceReference, created.ActorMemberId,
                    created.ActorDisplay, created.ChangedAt).IsSuccess);
                return Result.Success;
            });
        return created;
    }

    static async Task ApplyDraftAsync(OperationsFixture fixture, Uuid draftId,
        Func<CommitmentRules, Result> apply) =>
        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new CommitmentRules(fixture.TenantId, draftId), apply);

    static async Task ProjectAsync(FitzCommitmentDecisionWorkItemDirectory directory,
        IDomainEventReader events, Uuid tenantId, ICommitmentMutationContent requested)
    {
        var pattern = EventStreamPattern.ForPattern(tenantId.ToString(), "commitment-drafts");
        var identity = new CheckpointIdentity(
            FitzCommitmentDecisionWorkItemDirectory.ProjectorName, pattern);
        var current = await directory.LoadCheckpointAsync(tenantId);
        var source = await ReadCommitmentCheckpointAsync(events, tenantId);
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            current));
        var matches = new List<DomainEvent>();
        await foreach (var record in events.ReadAsync(pattern, EventCursor.Start, CancellationToken.None))
        {
            var selected = (requested, record.Event) switch
            {
                (CommitmentDraftCreatedContent expected, CommitmentDraftCreated actual) => expected.DraftId == actual.DraftId,
                (CommitmentDraftRevisedContent expected, CommitmentDraftRevised actual) => expected.DraftId == actual.DraftId && expected.Revision == actual.Revision,
                (CommitmentReviewedContent expected, CommitmentReviewed actual) => expected.DraftId == actual.DraftId && expected.DecisionId == actual.DecisionId,
                (CommitmentApprovedContent expected, CommitmentApproved actual) => expected.DraftId == actual.DraftId && expected.DecisionId == actual.DecisionId,
                (ResponsibilityAssigned expected, ResponsibilityAssigned actual) => expected.AssignmentId == actual.AssignmentId,
                (ResponsibilityRevoked expected, ResponsibilityRevoked actual) => expected.AssignmentId == actual.AssignmentId,
                _ => false,
            };
            if (selected)
                matches.Add(record.Event);
        }
        await directory.ApplyAsync(Assert.Single(matches));
        await batch.CommitAsync(source);
    }

    static async Task<ProjectionCheckpoint> ReadCommitmentCheckpointAsync(
        IDomainEventReader events, Uuid tenantId)
    {
        var cursor = EventCursor.Start;
        await foreach (var record in events.ReadAsync(EventStreamPattern.ForPattern(
                           tenantId.ToString(), "commitment-drafts"), cursor,
                           CancellationToken.None))
            cursor = record.NextCursor;
        return new ProjectionCheckpoint(cursor);
    }

    static DateTimeOffset At(DateOnly date) =>
        new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
