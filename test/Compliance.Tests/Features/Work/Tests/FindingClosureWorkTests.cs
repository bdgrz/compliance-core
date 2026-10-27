using Cntryl.Fitz.Testing;
using Microsoft.Extensions.DependencyInjection;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
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
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.ControlMappings.Readers;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Evaluations.Readers;
using Bdgrz.Compliance.Features.Evidence.Readers;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.Policies.Readers;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Readers;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Features.Work.Models;
using Bdgrz.Compliance.Features.Work.Queries;
using Bdgrz.Compliance.Features.Operations.Models;
namespace Bdgrz.Compliance.Tests.Features.Work.Tests;

public sealed class FindingClosureWorkTests
{
    [Fact]
    public async Task ShouldReconcileAssignedClosureCountsActionsAndNotificationsGivenSourceCompletion()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await ReadyAsync(fixture, fixture.OwnerUserId);
        finding = await fixture.AsAsync(fixture.LeadUserId, new ReviseFinding(Uuid.CreateVersion4(), fixture.TenantId,
            fixture.ProgramId, finding.FindingId, finding.Revision, "high", fixture.LeadMemberId,
            fixture.Today.AddDays(-7), "VPN", null, "Verification deadline passed."));
        var queue = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));
        var item = Assert.Single(queue.Items);
        await fixture.AsAsync(fixture.ApproverUserId, new AssignWorkItem(Uuid.CreateVersion4(), fixture.TenantId,
            fixture.ProgramId, item.WorkItemId, 0, fixture.ApproverMemberId));

        // Act
        var mine = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId));
        var detail = await fixture.AsAsync(fixture.ApproverUserId,
            new GetWorkItem(fixture.TenantId, fixture.ProgramId, item.WorkItemId));
        var reminders = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWorkReminders(fixture.TenantId, fixture.ProgramId));
        var digest = await fixture.AsAsync(fixture.ApproverUserId,
            new GetWorkDigest(fixture.TenantId, fixture.ProgramId));
        var closed = await fixture.CloseAsync(fixture.ApproverUserId, finding);
        var after = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "all"));
        var afterReminders = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWorkReminders(fixture.TenantId, fixture.ProgramId));
        var afterDigest = await fixture.AsAsync(fixture.ApproverUserId,
            new GetWorkDigest(fixture.TenantId, fixture.ProgramId));

        // Assert
        Assert.Equal(new WorkCountsView(1, 1, 0, 1), mine.Counts);
        Assert.Equal(Assert.Single(mine.Items), detail.Item);
        Assert.Equal(item.WorkItemId, Assert.Single(reminders).WorkItemId);
        Assert.Equal(item.WorkItemId, Assert.Single(digest.Overdue).WorkItemId);
        Assert.Equal(FindingStates.Closed, closed.Status);
        Assert.Equal(new WorkCountsView(0, 0, 0, 0), after.Counts);
        Assert.Empty(afterReminders);
        Assert.Empty(afterDigest.Overdue);
    }

    [Theory]
    [InlineData("finding_owner")]
    [InlineData("action_owner")]
    [InlineData("completer")]
    [InlineData("no_management")]
    public async Task ShouldOmitClosureCountsAndActionsGivenIneligibleActor(string role)
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await ReadyAsync(fixture,
            role == "completer" ? fixture.ApproverUserId : fixture.OwnerUserId);
        var actor = role switch
        {
            "finding_owner" => fixture.LeadUserId,
            "completer" => fixture.ApproverUserId,
            "no_management" => fixture.OutsiderUserId,
            _ => fixture.OwnerUserId,
        };
        var candidate = FindingClosureWork.Candidate(finding)!;

        // Act
        var queue = await fixture.AsAsync(actor, new ListWork(fixture.TenantId,
            fixture.ProgramId, "all"));
        var detail = await fixture.Scenario(actor).When(new GetWorkItem(fixture.TenantId,
            fixture.ProgramId, candidate.WorkItemId)).ExpectFailure(RequestErrorKind.NotFound);
        var reminders = await fixture.AsAsync(actor,
            new ListWorkReminders(fixture.TenantId, fixture.ProgramId));
        var digest = await fixture.AsAsync(actor,
            new GetWorkDigest(fixture.TenantId, fixture.ProgramId));

        // Assert
        Assert.Equal(new WorkCountsView(0, 0, 0, 0), queue.Counts);
        Assert.Empty(queue.Items);
        Assert.False(detail.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, detail.Error.Kind);
        Assert.Empty(reminders);
        Assert.Empty(digest.DueSoon);
        Assert.Empty(digest.Overdue);
    }

    [Fact]
    public async Task ShouldDropPreviousAssignmentGivenFindingRevisionChange()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await ReadyAsync(fixture, fixture.OwnerUserId);
        var candidate = FindingClosureWork.Candidate(finding)!;
        await fixture.AsAsync(fixture.ApproverUserId, new AssignWorkItem(Uuid.CreateVersion4(), fixture.TenantId,
            fixture.ProgramId, candidate.WorkItemId, 0, fixture.ApproverMemberId));

        // Act
        finding = await fixture.AsAsync(fixture.LeadUserId, new ReviseFinding(Uuid.CreateVersion4(), fixture.TenantId,
            fixture.ProgramId, finding.FindingId, finding.Revision, "medium", fixture.LeadMemberId,
            finding.DueOn, "VPN", null, "Severity reassessed."));
        var queue = await fixture.AsAsync(fixture.ApproverUserId,
            new ListWork(fixture.TenantId, fixture.ProgramId, "unassigned"));
        var previous = await fixture.Scenario(fixture.ApproverUserId).When(
            new GetWorkItem(fixture.TenantId, fixture.ProgramId, candidate.WorkItemId))
            .ExpectFailure(RequestErrorKind.NotFound);

        // Assert
        var item = Assert.Single(queue.Items);
        Assert.NotEqual(candidate.WorkItemId, item.WorkItemId);
        Assert.Equal(FindingClosureWork.Candidate(finding)!.WorkItemId, item.WorkItemId);
        Assert.Null(item.AssigneeMemberId);
        Assert.False(previous.IsSuccess);
        Assert.Equal(RequestErrorKind.NotFound, previous.Error.Kind);
    }

    [Fact]
    public async Task ShouldRejectAssignmentGivenSourceInvolvedMember()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await ReadyAsync(fixture, fixture.OwnerUserId);
        var candidate = FindingClosureWork.Candidate(finding)!;

        // Act
        var assigned = await fixture.Scenario(fixture.ApproverUserId).When(
            new AssignWorkItem(Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, candidate.WorkItemId,
                0, fixture.OwnerMemberId)).ExpectFailure(RequestErrorKind.Validation);

        // Assert
        Assert.False(assigned.IsSuccess);
        Assert.Equal(RequestErrorKind.Validation, assigned.Error.Kind);
    }

    [Fact]
    public async Task ShouldFenceClosureCountsGivenLaggingOrChangingSourceProjection()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await ReadyAsync(fixture, fixture.OwnerUserId);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        await using var findingScope = fixture.Provider.CreateAsyncScope();
        var directory = new FitzFindingClosureWorkItemDirectory(new InMemoryKvClient(),
            findingScope.ServiceProvider.GetRequiredService<FindingReader>(), TimeProvider.System);
        var consistency = new WorkQueueReadConsistency(events, [directory]);
        var lagging = await consistency.CaptureAsync(fixture.TenantId, CancellationToken.None);
        var pattern = directory.SourcePattern(fixture.TenantId);
        await using (var batch = await directory.BeginAsync(new ProjectionBatchContext(
                         new CheckpointIdentity(FitzFindingClosureWorkItemDirectory.ProjectorName,
                             pattern), ProjectionCheckpoint.Start)))
        {
            var cursor = ProjectionCheckpoint.Start.Cursor;
            await foreach (var record in events.ReadAsync(pattern, cursor, CancellationToken.None))
            {
                await directory.ApplyAsync(record.Event);
                cursor = record.NextCursor;
            }
            await batch.CommitAsync(new ProjectionCheckpoint(cursor));
        }
        var captured = await consistency.CaptureAsync(fixture.TenantId, CancellationToken.None);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var queue = new WorkQueueReader(services.GetRequiredService<IAggregateReader>(), services.GetRequiredService<WorkAssignmentReader>(), services.GetRequiredService<ControlReader>(), services.GetRequiredService<PolicyReader>(),
            services.GetRequiredService<OperatingAuthority>(), TimeProvider.System, consistency,
            accountableWorkItems: [directory],
            operatingReader: services.GetRequiredService<ControlOperationsReader>(),
            mappingReader: services.GetRequiredService<ControlMappingReader>(),
            applicabilityReader: services.GetRequiredService<ApplicabilityReader>(),
            evaluationReader: services.GetRequiredService<ControlEvaluationReader>(),
            riskGovernance: services.GetRequiredService<Bdgrz.Compliance.Features.Risks.Readers.RiskGovernanceReader>(),
            evidenceRequests: services.GetRequiredService<Bdgrz.Compliance.Features.Evidence.Readers.EvidenceRequestReader>());

        // Act
        var read = await queue.ReadAsync(fixture.TenantId, fixture.ProgramId,
            new OperationsActor(fixture.ApproverUserId, fixture.ApproverMemberId, "Reviewer"),
            30, CancellationToken.None);
        await fixture.AsAsync(fixture.LeadUserId, new ReviseFinding(Uuid.CreateVersion4(), fixture.TenantId,
            fixture.ProgramId, finding.FindingId, finding.Revision, "medium", fixture.LeadMemberId,
            finding.DueOn, "VPN", null, "Review inputs changed."));
        var changed = await consistency.ConfirmUnchangedAndCaughtUpAsync(fixture.TenantId,
            captured.Value, CancellationToken.None);

        // Assert
        Assert.False(lagging.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, lagging.Error.Kind);
        Assert.True(lagging.Error.IsTransient);
        Assert.True(captured.IsSuccess);
        Assert.True(read.IsSuccess);
        var entry = Assert.Single(read.Value.Entries);
        Assert.Equal(FindingClosureWork.Kind, entry.Item.Kind);
        Assert.Equal(finding.FindingId, entry.Item.SourceId);
        Assert.False(changed.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error.Kind);
        Assert.True(changed.Error.IsTransient);
    }

    static async Task<FindingView> ReadyAsync(OperationsFixture fixture, Uuid completerUserId)
    {
        var finding = await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId,
            fixture.Today);
        return await fixture.AsAsync(completerUserId, fixture.Complete(finding, Assert.Single(finding.CorrectiveActions).ActionId, "Removed obsolete access.", OperationsFixture.FullSupport));
    }
}
