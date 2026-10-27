using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Models;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evaluations.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Remediation.Services;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Processes;
using Bdgrz.Compliance.Features.Work.Models;
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
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Handlers;
using Bdgrz.Compliance.Features.ControlMappings.Aggregates;
using Bdgrz.Compliance.Features.ControlMappings.Readers;
using Bdgrz.Compliance.Features.Controls.Readers;
using Bdgrz.Compliance.Features.Evaluations.Commands;
using Bdgrz.Compliance.Features.Evaluations.Models;
using Bdgrz.Compliance.Features.Evaluations.Readers;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Readers;
using Bdgrz.Compliance.Features.Operations.Aggregates;
using Bdgrz.Compliance.Features.Operations.Readers;
using Bdgrz.Compliance.Features.Policies.Readers;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Risks.Aggregates;
using Bdgrz.Compliance.Features.Risks.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Authorizers;
using Bdgrz.Compliance.Features.Work.Commands;
using Bdgrz.Compliance.Tests;
namespace Bdgrz.Compliance.Tests.Features.Work.Tests;

public sealed class WorkQueueReadConsistencyTests
{
    [Fact]
    public async Task ShouldIgnoreUnrelatedTenantChangeGivenCaughtUpWorkProjection()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.CatchUpBoundaryDirectoryAsync();
        fixture.Boundaries.FollowSource = false;
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var evidence = new ProjectedWorkItems(ProjectionCheckpoint.Start);
        var consistency = new WorkQueueReadConsistency(events, [evidence]);
        var captured = await consistency.CaptureAsync(fixture.TenantId, CancellationToken.None);
        Assert.True(captured.IsSuccess);

        await ProgramManagementServices.SeedAsync(fixture.Provider,
            new Member(fixture.TenantId, Uuid.CreateVersion4()), member =>
            {
                member.Register();
                return Result.Success;
            });

        // Act
        var confirmed = await consistency.ConfirmUnchangedAndCaughtUpAsync(fixture.TenantId,
            captured.Value, CancellationToken.None);

        // Assert
        Assert.True(confirmed.IsSuccess);
    }

    [Fact]
    public async Task ShouldRejectLaggingEvidenceProjectionGivenPendingSourceEvent()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await SeedEvidenceRequestAsync(fixture);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var evidence = new ProjectedWorkItems(ProjectionCheckpoint.Start);
        var consistency = new WorkQueueReadConsistency(events, [evidence]);

        // Act
        var captured = await consistency.CaptureAsync(fixture.TenantId, CancellationToken.None);

        // Assert
        Assert.False(captured.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, captured.Error.Kind);
        Assert.True(captured.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldAcceptEvidenceProjectionAtSourceCheckpointGivenNoPendingEvents()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        await SeedEvidenceRequestAsync(fixture);
        var evidenceCheckpoint = await ReadCheckpointAsync(events, fixture.TenantId,
            "evidence-requests");
        var evidence = new ProjectedWorkItems(evidenceCheckpoint);
        var consistency = new WorkQueueReadConsistency(events, [evidence]);

        // Act
        var captured = await consistency.CaptureAsync(fixture.TenantId, CancellationToken.None);
        var confirmed = captured.IsSuccess
            ? await consistency.ConfirmUnchangedAndCaughtUpAsync(fixture.TenantId,
                captured.Value, CancellationToken.None)
            : Result.Failure(captured.Error);

        // Assert
        Assert.True(captured.IsSuccess);
        Assert.True(confirmed.IsSuccess);
    }

    [Fact]
    public async Task ShouldRejectLaggingCorrectiveActionProjectionGivenPendingSourceEvent()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId,
            fixture.Today.AddDays(5));
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var correctiveActions = new ProjectedWorkItems(ProjectionCheckpoint.Start,
            area: "findings", kind: WorkSource.CorrectiveAction,
            projectorName: "TestCorrectiveActionWorkItems");
        var consistency = new WorkQueueReadConsistency(events,
            [correctiveActions]);

        // Act
        var captured = await consistency.CaptureAsync(fixture.TenantId, CancellationToken.None);

        // Assert
        Assert.False(captured.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, captured.Error.Kind);
        Assert.True(captured.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldRejectLaggingRiskGovernanceProjectionGivenPendingSourceEvent()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var now = DateTimeOffset.UtcNow;
        await RiskSeeds.AddActionAsync(fixture.Provider, fixture.TenantId, fixture.ProgramId, Uuid.CreateVersion4(), Uuid.CreateVersion4(), "Enforce MFA", "MFA is required", "Identity provider policy export.", fixture.Today.AddDays(5), fixture.OwnerMemberId, [], ActorReference.ForMember(fixture.LeadMemberId, "Lead"), now);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var riskWork = new ProjectedWorkItems(ProjectionCheckpoint.Start,
            area: RiskTreatmentAction.Area, kind: WorkSource.RiskTreatmentAction,
            projectorName: "TestRiskWorkItems", kinds:
            [
                WorkSource.RiskTreatmentAction,
                WorkSource.RiskTreatmentActionReview,
                WorkSource.RiskControlTreatmentReview,
            ]);
        var consistency = new WorkQueueReadConsistency(events, [riskWork]);

        // Act
        var captured = await consistency.CaptureAsync(fixture.TenantId, CancellationToken.None);

        // Assert
        Assert.False(captured.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, captured.Error.Kind);
        Assert.True(captured.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldRejectLaggingControlEvaluationProjectionGivenPendingSourceEvent()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await SubmitControlEvaluationAsync(fixture);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var evaluationWork = new ProjectedWorkItems(ProjectionCheckpoint.Start,
            area: "control-evaluations", kind: WorkSource.ControlEvaluationReview,
            projectorName: "TestControlEvaluationWorkItems");
        var consistency = new WorkQueueReadConsistency(events,
            [evaluationWork]);

        // Act
        var captured = await consistency.CaptureAsync(fixture.TenantId, CancellationToken.None);

        // Assert
        Assert.False(captured.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, captured.Error.Kind);
        Assert.True(captured.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldRejectLaggingCriterionApplicabilityProjectionGivenPendingSourceEvent()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await SeedCriterionApplicabilityProposalAsync(fixture);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var applicabilityWork = new ProjectedWorkItems(ProjectionCheckpoint.Start,
            area: "criterion-applicability", kind: WorkSource.CriterionApplicabilityReview,
            projectorName: "TestCriterionApplicabilityWorkItems");
        var consistency = new WorkQueueReadConsistency(events,
            [applicabilityWork]);

        // Act
        var captured = await consistency.CaptureAsync(fixture.TenantId, CancellationToken.None);

        // Assert
        Assert.False(captured.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, captured.Error.Kind);
        Assert.True(captured.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldRejectLaggingControlDecisionProjectionGivenPendingSourceEvent()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var decisions = new FitzControlDecisionWorkItemDirectory(new InMemoryKvClient(),
            services.GetRequiredService<ControlReader>(),
            services.GetRequiredService<OperatingAuthority>(),
            new ControlActivationReleaseGate(true), new ControlLifecycleReleaseGate(true));
        var consistency = new WorkQueueReadConsistency(events, [decisions]);

        // Act
        var captured = await consistency.CaptureAsync(fixture.TenantId, CancellationToken.None);

        // Assert
        Assert.False(captured.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, captured.Error.Kind);
        Assert.True(captured.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldRejectLaggingControlMappingProjectionGivenPendingSourceEvent()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await SeedControlMappingProposalAsync(fixture);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var mappingWork = new ProjectedWorkItems(ProjectionCheckpoint.Start,
            area: "control-criterion-mappings", kind: WorkSource.ControlCriterionMappingReview,
            projectorName: "TestControlMappingWorkItems");
        var consistency = new WorkQueueReadConsistency(events, [mappingWork]);

        // Act
        var captured = await consistency.CaptureAsync(fixture.TenantId, CancellationToken.None);

        // Assert
        Assert.False(captured.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, captured.Error.Kind);
        Assert.True(captured.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldRejectChangedCorrectiveActionProjectionGivenReadFence()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var correctiveActions = new ProjectedWorkItems(ProjectionCheckpoint.Start,
            area: "findings", kind: WorkSource.CorrectiveAction,
            projectorName: "TestCorrectiveActionWorkItems");
        var consistency = new WorkQueueReadConsistency(events,
            [correctiveActions]);
        var captured = await consistency.CaptureAsync(fixture.TenantId, CancellationToken.None);
        Assert.True(captured.IsSuccess);
        correctiveActions.Checkpoint = new ProjectionCheckpoint(new EventCursor("advanced"));

        // Act
        var confirmed = await consistency.ConfirmUnchangedAndCaughtUpAsync(fixture.TenantId,
            captured.Value, CancellationToken.None);

        // Assert
        Assert.False(confirmed.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, confirmed.Error.Kind);
        Assert.True(confirmed.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldListProjectedEvidenceWorkGivenCaughtUpProjection()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var evidenceCheckpoint = await ReadCheckpointAsync(events, fixture.TenantId,
            "evidence-requests");
        var requestId = Uuid.CreateVersion4();
        var candidate = new WorkCandidate(WorkCandidate.IdFor(requestId, "evidence_request"),
            "evidence_request", requestId, null, null, "Quarterly access export",
            "Upload the approved access export.", fixture.Today.AddDays(5), null, "fulfil",
            $"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/" +
            $"evidence-requests/{requestId}/fulfilments",
            new OperatingHolder(OperatingAuthority.MemberHolder, fixture.OwnerMemberId), null,
            new HashSet<Uuid>(), DateTimeOffset.UtcNow);
        var evidence = new ProjectedWorkItems(evidenceCheckpoint, [candidate]);
        var consistency = new WorkQueueReadConsistency(events, [evidence]);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var queue = new WorkQueueReader(services.GetRequiredService<IAggregateReader>(), services.GetRequiredService<WorkAssignmentReader>(), services.GetRequiredService<ControlReader>(), services.GetRequiredService<PolicyReader>(),
            services.GetRequiredService<OperatingAuthority>(), TimeProvider.System, consistency,
            accountableWorkItems: [evidence],
            operatingReader: services.GetRequiredService<ControlOperationsReader>(),
            mappingReader: services.GetRequiredService<ControlMappingReader>(),
            applicabilityReader: services.GetRequiredService<ApplicabilityReader>(),
            evaluationReader: services.GetRequiredService<ControlEvaluationReader>(),
            riskGovernance: services.GetRequiredService<Bdgrz.Compliance.Features.Risks.Readers.RiskGovernanceReader>(),
            evidenceRequests: services.GetRequiredService<Bdgrz.Compliance.Features.Evidence.Readers.EvidenceRequestReader>());
        var actor = new OperationsActor(fixture.OwnerUserId, fixture.OwnerMemberId, "Owner");

        // Act
        var read = await queue.ReadAsync(fixture.TenantId, fixture.ProgramId, actor, 30,
            CancellationToken.None);

        // Assert
        Assert.True(read.IsSuccess);
        var entry = Assert.Single(read.Value.Entries,
            item => item.Candidate.Kind == "evidence_request");
        Assert.Equal(requestId, entry.Candidate.SourceId);
        Assert.Equal("Quarterly access export", entry.Item.Summary);
    }

    [Fact]
    public async Task ShouldListProjectedCorrectiveActionWithoutLiveDuplicateGivenCaughtUpProjection()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var finding = await WorkTestData.AddActionsAsync(fixture, fixture.OwnerMemberId,
            fixture.Today.AddDays(5));
        var action = Assert.Single(finding.CorrectiveActions);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var checkpoint = await ReadCheckpointAsync(events, fixture.TenantId, "findings");
        var candidate = new WorkCandidate(WorkCandidate.IdFor(action.ActionId,
                WorkSource.CorrectiveAction), WorkSource.CorrectiveAction, action.ActionId,
            null, finding.FindingId, action.Description,
            $"Corrective action for finding \"{finding.Title}\".", action.DueOn,
            FindingStates.Materiality(finding.Severity), "complete",
            $"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/" +
            $"findings/{finding.FindingId}/corrective-actions/{action.ActionId}/completions",
            new OperatingHolder(OperatingAuthority.MemberHolder, action.OwnerMemberId), null,
            new HashSet<Uuid>(), action.AddedAt);
        var correctiveActions = new ProjectedWorkItems(checkpoint, [candidate], "findings",
            WorkSource.CorrectiveAction, "TestCorrectiveActionWorkItems");
        var consistency = new WorkQueueReadConsistency(events,
            [correctiveActions]);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var queue = new WorkQueueReader(services.GetRequiredService<IAggregateReader>(), services.GetRequiredService<WorkAssignmentReader>(), services.GetRequiredService<ControlReader>(), services.GetRequiredService<PolicyReader>(),
            services.GetRequiredService<OperatingAuthority>(), TimeProvider.System, consistency,
            accountableWorkItems: [correctiveActions],
            operatingReader: services.GetRequiredService<ControlOperationsReader>(),
            mappingReader: services.GetRequiredService<ControlMappingReader>(),
            applicabilityReader: services.GetRequiredService<ApplicabilityReader>(),
            evaluationReader: services.GetRequiredService<ControlEvaluationReader>(),
            riskGovernance: services.GetRequiredService<Bdgrz.Compliance.Features.Risks.Readers.RiskGovernanceReader>(),
            evidenceRequests: services.GetRequiredService<Bdgrz.Compliance.Features.Evidence.Readers.EvidenceRequestReader>());
        var actor = new OperationsActor(fixture.OwnerUserId, fixture.OwnerMemberId, "Owner");

        // Act
        var read = await queue.ReadAsync(fixture.TenantId, fixture.ProgramId, actor, 30,
            CancellationToken.None);

        // Assert
        Assert.True(read.IsSuccess);
        var entry = Assert.Single(read.Value.Entries,
            item => item.Candidate.Kind == WorkSource.CorrectiveAction);
        Assert.Equal(action.ActionId, entry.Candidate.SourceId);
        Assert.Equal(action.Description, entry.Item.Summary);
    }

    [Fact]
    public async Task ShouldListProjectedRiskActionWithoutLiveDuplicateGivenCaughtUpProjection()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var riskId = Uuid.CreateVersion4();
        var actionId = Uuid.CreateVersion4();
        var now = DateTimeOffset.UtcNow;
        var actor = ActorReference.ForMember(fixture.LeadMemberId, "Lead");
        await RiskSeeds.AddActionAsync(fixture.Provider, fixture.TenantId, fixture.ProgramId, riskId, actionId, "Enforce MFA", "MFA is required", "Identity provider policy export.", fixture.Today.AddDays(5), fixture.OwnerMemberId, [], actor, now);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var checkpoint = await ReadCheckpointAsync(events, fixture.TenantId, RiskTreatmentAction.Area);
        var candidate = new WorkCandidate(WorkSource.RiskTreatmentWorkItemId(fixture.ProgramId,
                riskId, actionId, WorkSource.RiskTreatmentAction), WorkSource.RiskTreatmentAction,
            actionId,
            null, null, "Enforce MFA", "Treatment work for a risk. Target state: MFA is required",
            fixture.Today.AddDays(5), null, "complete",
            $"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/" +
            $"risks/{riskId}/treatment-actions/{actionId}/completions",
            new OperatingHolder(OperatingAuthority.MemberHolder, fixture.OwnerMemberId), null,
            new HashSet<Uuid>(), now);
        var riskWork = new ProjectedWorkItems(checkpoint, [candidate], RiskTreatmentAction.Area,
            WorkSource.RiskTreatmentAction, "TestRiskWorkItems",
            [WorkSource.RiskTreatmentAction, WorkSource.RiskTreatmentActionReview,
                WorkSource.RiskControlTreatmentReview]);
        var consistency = new WorkQueueReadConsistency(events, [riskWork]);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var queue = new WorkQueueReader(services.GetRequiredService<IAggregateReader>(), services.GetRequiredService<WorkAssignmentReader>(), services.GetRequiredService<ControlReader>(), services.GetRequiredService<PolicyReader>(),
            services.GetRequiredService<OperatingAuthority>(), TimeProvider.System, consistency,
            accountableWorkItems: [riskWork],
            operatingReader: services.GetRequiredService<ControlOperationsReader>(),
            mappingReader: services.GetRequiredService<ControlMappingReader>(),
            applicabilityReader: services.GetRequiredService<ApplicabilityReader>(),
            evaluationReader: services.GetRequiredService<ControlEvaluationReader>(),
            riskGovernance: services.GetRequiredService<Bdgrz.Compliance.Features.Risks.Readers.RiskGovernanceReader>(),
            evidenceRequests: services.GetRequiredService<Bdgrz.Compliance.Features.Evidence.Readers.EvidenceRequestReader>());
        var actorContext = new OperationsActor(fixture.OwnerUserId, fixture.OwnerMemberId, "Owner");

        // Act
        var read = await queue.ReadAsync(fixture.TenantId, fixture.ProgramId, actorContext, 30,
            CancellationToken.None);

        // Assert
        Assert.True(read.IsSuccess);
        var entry = Assert.Single(read.Value.Entries,
            item => item.Candidate.Kind == WorkSource.RiskTreatmentAction);
        Assert.Equal(actionId, entry.Candidate.SourceId);
        Assert.Equal("Enforce MFA", entry.Item.Summary);
    }

    [Fact]
    public async Task ShouldListProjectedControlEvaluationReviewWithoutLiveDuplicateGivenCaughtUpProjection()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var evaluation = await SubmitControlEvaluationAsync(fixture);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var checkpoint = await ReadCheckpointAsync(events, fixture.TenantId, "control-evaluations");
        var identity = Uuid.CreateVersion5(evaluation.EvaluationId,
            $"control-evaluation-review\n{evaluation.Round}");
        var candidate = new WorkCandidate(WorkCandidate.IdFor(identity,
                WorkSource.ControlEvaluationReview), WorkSource.ControlEvaluationReview,
            evaluation.EvaluationId, evaluation.ControlId, null, "Review control evaluation",
            $"Control evaluation round {evaluation.Round} is awaiting independent review.", null,
            null, "review",
            $"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/controls/" +
            $"{evaluation.ControlId}/evaluations/{evaluation.EvaluationId}/reviews",
            new OperatingHolder(OperatingAuthority.ProgramReviewerHolder, fixture.ProgramId), null,
            new HashSet<Uuid> { evaluation.EvaluatorMemberId }, evaluation.Submissions[^1].SubmittedAt);
        var evaluationWork = new ProjectedWorkItems(checkpoint, [candidate], "control-evaluations",
            WorkSource.ControlEvaluationReview, "TestControlEvaluationWorkItems");
        var consistency = new WorkQueueReadConsistency(events,
            [evaluationWork]);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var queue = new WorkQueueReader(services.GetRequiredService<IAggregateReader>(), services.GetRequiredService<WorkAssignmentReader>(), services.GetRequiredService<ControlReader>(), services.GetRequiredService<PolicyReader>(),
            services.GetRequiredService<OperatingAuthority>(), TimeProvider.System, consistency,
            accountableWorkItems: [evaluationWork],
            operatingReader: services.GetRequiredService<ControlOperationsReader>(),
            mappingReader: services.GetRequiredService<ControlMappingReader>(),
            applicabilityReader: services.GetRequiredService<ApplicabilityReader>(),
            evaluationReader: services.GetRequiredService<ControlEvaluationReader>(),
            riskGovernance: services.GetRequiredService<Bdgrz.Compliance.Features.Risks.Readers.RiskGovernanceReader>(),
            evidenceRequests: services.GetRequiredService<Bdgrz.Compliance.Features.Evidence.Readers.EvidenceRequestReader>());
        var actor = new OperationsActor(fixture.LeadUserId, fixture.LeadMemberId, "Lead");

        // Act
        var read = await queue.ReadAsync(fixture.TenantId, fixture.ProgramId, actor, 30,
            CancellationToken.None);

        // Assert
        Assert.True(read.IsSuccess);
        var entry = Assert.Single(read.Value.Entries,
            item => item.Candidate.Kind == WorkSource.ControlEvaluationReview);
        Assert.Equal(evaluation.EvaluationId, entry.Candidate.SourceId);
        Assert.Equal("Review control evaluation", entry.Item.Summary);
    }

    [Fact]
    public async Task ShouldListProjectedCriterionApplicabilityReviewWithoutLiveDuplicateGivenCaughtUpProjection()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var (decisionId, proposedAt) =
            await SeedCriterionApplicabilityProposalAsync(fixture);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var checkpoint = await ReadCheckpointAsync(events, fixture.TenantId,
            "criterion-applicability");
        var identity = Uuid.CreateVersion5(decisionId, "criterion-applicability-review\n1");
        var candidate = new WorkCandidate(WorkCandidate.IdFor(identity,
                WorkSource.CriterionApplicabilityReview), WorkSource.CriterionApplicabilityReview,
            decisionId, null, null, "Review not-applicable proposal for AC-2.4",
            "A criterion not-applicable proposal is awaiting independent review.", null, null,
            "review", $"/api/v1/tenants/{fixture.TenantId}/programs/{fixture.ProgramId}/" +
            $"criterion-applicability/{decisionId}/reviews",
            new OperatingHolder(OperatingAuthority.ProgramReviewerHolder, fixture.ProgramId), null,
            new HashSet<Uuid> { fixture.OwnerMemberId }, proposedAt);
        var applicabilityWork = new ProjectedWorkItems(checkpoint, [candidate],
            "criterion-applicability", WorkSource.CriterionApplicabilityReview,
            "TestCriterionApplicabilityWorkItems");
        var consistency = new WorkQueueReadConsistency(events,
            [applicabilityWork]);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var queue = new WorkQueueReader(services.GetRequiredService<IAggregateReader>(), services.GetRequiredService<WorkAssignmentReader>(), services.GetRequiredService<ControlReader>(), services.GetRequiredService<PolicyReader>(),
            services.GetRequiredService<OperatingAuthority>(), TimeProvider.System, consistency,
            accountableWorkItems: [applicabilityWork],
            operatingReader: services.GetRequiredService<ControlOperationsReader>(),
            mappingReader: services.GetRequiredService<ControlMappingReader>(),
            applicabilityReader: services.GetRequiredService<ApplicabilityReader>(),
            evaluationReader: services.GetRequiredService<ControlEvaluationReader>(),
            riskGovernance: services.GetRequiredService<Bdgrz.Compliance.Features.Risks.Readers.RiskGovernanceReader>(),
            evidenceRequests: services.GetRequiredService<Bdgrz.Compliance.Features.Evidence.Readers.EvidenceRequestReader>());
        var actor = new OperationsActor(fixture.LeadUserId, fixture.LeadMemberId, "Lead");

        // Act
        var read = await queue.ReadAsync(fixture.TenantId, fixture.ProgramId, actor, 30,
            CancellationToken.None);

        // Assert
        Assert.True(read.IsSuccess);
        var entry = Assert.Single(read.Value.Entries,
            item => item.Candidate.Kind == WorkSource.CriterionApplicabilityReview);
        Assert.Equal(decisionId, entry.Candidate.SourceId);
        Assert.Null(entry.Candidate.ControlId);
        Assert.Equal("Review not-applicable proposal for AC-2.4", entry.Item.Summary);
    }

    [Fact]
    public async Task ShouldListProjectedControlMappingReviewWithoutLiveDuplicateGivenCaughtUpProjection()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var (mappingId, proposedAt, editionId) = await SeedControlMappingProposalAsync(fixture);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var checkpoint = await ReadCheckpointAsync(events, fixture.TenantId,
            "control-criterion-mappings");
        var candidate = WorkSource.ControlCriterionMappingReviewCandidate(fixture.TenantId,
            fixture.ProgramId, ControlCriterionMapping.IdentityOf(fixture.TenantId, fixture.ProgramId, fixture.ControlId,
                editionId, "AC-2.4"), fixture.ControlId, mappingId, "AC-2.4", fixture.OwnerMemberId, 1,
            proposedAt);
        var mappingWork = new ProjectedWorkItems(checkpoint, [candidate],
            "control-criterion-mappings", WorkSource.ControlCriterionMappingReview,
            "TestControlMappingWorkItems");
        var consistency = new WorkQueueReadConsistency(events, [mappingWork]);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var queue = new WorkQueueReader(services.GetRequiredService<IAggregateReader>(), services.GetRequiredService<WorkAssignmentReader>(), services.GetRequiredService<ControlReader>(), services.GetRequiredService<PolicyReader>(),
            services.GetRequiredService<OperatingAuthority>(), TimeProvider.System, consistency,
            accountableWorkItems: [mappingWork],
            operatingReader: services.GetRequiredService<ControlOperationsReader>(),
            mappingReader: services.GetRequiredService<ControlMappingReader>(),
            applicabilityReader: services.GetRequiredService<ApplicabilityReader>(),
            evaluationReader: services.GetRequiredService<ControlEvaluationReader>(),
            riskGovernance: services.GetRequiredService<Bdgrz.Compliance.Features.Risks.Readers.RiskGovernanceReader>(),
            evidenceRequests: services.GetRequiredService<Bdgrz.Compliance.Features.Evidence.Readers.EvidenceRequestReader>());
        var actor = new OperationsActor(fixture.LeadUserId, fixture.LeadMemberId, "Lead");

        // Act
        var read = await queue.ReadAsync(fixture.TenantId, fixture.ProgramId, actor, 30,
            CancellationToken.None);

        // Assert
        Assert.True(read.IsSuccess);
        var entry = Assert.Single(read.Value.Entries,
            item => item.Candidate.Kind == WorkSource.ControlCriterionMappingReview);
        Assert.Equal(mappingId, entry.Candidate.SourceId);
        Assert.Equal(fixture.ControlId, entry.Candidate.ControlId);
        Assert.Equal("Review control mapping for AC-2.4", entry.Item.Summary);
    }

    [Fact]
    public async Task ShouldRejectLaggingOperatingPlanProjectionGivenPendingSourceEvent()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.ProposeAsync(0);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var planWork = new ProjectedWorkItems(ProjectionCheckpoint.Start,
            area: ControlOperatingPlan.Area, kind: WorkSource.ControlOperatingPlanApproval,
            projectorName: "TestControlOperatingPlanWorkItems");
        var consistency = new WorkQueueReadConsistency(events, [planWork]);

        // Act
        var captured = await consistency.CaptureAsync(fixture.TenantId, CancellationToken.None);

        // Assert
        Assert.False(captured.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, captured.Error.Kind);
        Assert.True(captured.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldRejectLaggingOccurrenceProjectionGivenPendingSourceEvent()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync();
        await fixture.CatchUpBoundaryDirectoryAsync();
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var occurrenceWork = new FitzControlOccurrenceWorkItemDirectory(new InMemoryKvClient(),
            sourceScope.ServiceProvider.GetRequiredService<ControlReader>(),
                sourceScope.ServiceProvider.GetRequiredService<IImmutableContentStore>());
        var consistency = new WorkQueueReadConsistency(events,
            [occurrenceWork]);

        // Act
        var captured = await consistency.CaptureAsync(fixture.TenantId, CancellationToken.None);

        // Assert
        Assert.False(captured.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, captured.Error.Kind);
        Assert.True(captured.Error.IsTransient);
    }

    [Fact]
    public async Task ShouldListProjectedOccurrenceWithoutLiveDuplicateGivenCaughtUpProjection()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        await fixture.PlanAsync();
        var occurrence = (await fixture.OccurrencesAsync())
            .First(item => item.PeriodStart <= fixture.Today);
        await fixture.CatchUpBoundaryDirectoryAsync();
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        await using var sourceScope = fixture.Provider.CreateAsyncScope();
        var services = sourceScope.ServiceProvider;
        var occurrenceWork = new FitzControlOccurrenceWorkItemDirectory(new InMemoryKvClient(),
            services.GetRequiredService<ControlReader>(),
                services.GetRequiredService<IImmutableContentStore>());
        await FitzControlOccurrenceWorkItemDirectoryTests.CatchUpAsync(fixture, occurrenceWork);
        var consistency = new WorkQueueReadConsistency(events,
            [occurrenceWork]);
        var queue = new WorkQueueReader(services.GetRequiredService<IAggregateReader>(), services.GetRequiredService<WorkAssignmentReader>(), services.GetRequiredService<ControlReader>(), services.GetRequiredService<PolicyReader>(),
            services.GetRequiredService<OperatingAuthority>(), TimeProvider.System, consistency,
            accountableWorkItems: [occurrenceWork],
            operatingReader: services.GetRequiredService<ControlOperationsReader>(),
            mappingReader: services.GetRequiredService<ControlMappingReader>(),
            applicabilityReader: services.GetRequiredService<ApplicabilityReader>(),
            evaluationReader: services.GetRequiredService<ControlEvaluationReader>(),
            riskGovernance: services.GetRequiredService<Bdgrz.Compliance.Features.Risks.Readers.RiskGovernanceReader>(),
            evidenceRequests: services.GetRequiredService<Bdgrz.Compliance.Features.Evidence.Readers.EvidenceRequestReader>());
        var actor = new OperationsActor(fixture.OwnerUserId, fixture.OwnerMemberId, "Owner");

        // Act
        var read = await queue.ReadAsync(fixture.TenantId, fixture.ProgramId, actor, 30,
            CancellationToken.None);

        // Assert
        Assert.True(read.IsSuccess);
        var entry = Assert.Single(read.Value.Entries,
            item => item.Candidate.Kind == WorkSource.ControlOccurrence &&
                    item.Candidate.SourceId == occurrence.OccurrenceId);
        Assert.True(entry.ActorEligible);
        Assert.Equal("attest", entry.Candidate.NextAction);
    }

    [Fact]
    public async Task ShouldListProjectedOperatingPlanApprovalWithoutLiveDuplicateGivenCaughtUpProjection()
    {
        // Arrange
        var fixture = await OperationsFixture.CreateAsync();
        var plan = await fixture.ProposeAsync(0);
        var events = fixture.Provider.GetRequiredService<IDomainEventReader>();
        var checkpoint = await ReadCheckpointAsync(events, fixture.TenantId, "control-operations");
        var candidate = WorkSource.ControlOperatingPlanApprovalCandidate(fixture.TenantId,
            fixture.ProgramId, fixture.ControlId, "AC-OPS", plan.PlanVersionId,
            fixture.LeadMemberId, plan.ProposedAt);
        var planWork = new ProjectedWorkItems(checkpoint, [candidate], "control-operations",
            WorkSource.ControlOperatingPlanApproval, "TestControlOperatingPlanWorkItems");
        var consistency = new WorkQueueReadConsistency(events, [planWork]);
        await using var scope = fixture.Provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var queue = new WorkQueueReader(services.GetRequiredService<IAggregateReader>(), services.GetRequiredService<WorkAssignmentReader>(), services.GetRequiredService<ControlReader>(), services.GetRequiredService<PolicyReader>(),
            services.GetRequiredService<OperatingAuthority>(), TimeProvider.System, consistency,
            accountableWorkItems: [planWork],
            operatingReader: services.GetRequiredService<ControlOperationsReader>(),
            mappingReader: services.GetRequiredService<ControlMappingReader>(),
            applicabilityReader: services.GetRequiredService<ApplicabilityReader>(),
            evaluationReader: services.GetRequiredService<ControlEvaluationReader>(),
            riskGovernance: services.GetRequiredService<Bdgrz.Compliance.Features.Risks.Readers.RiskGovernanceReader>(),
            evidenceRequests: services.GetRequiredService<Bdgrz.Compliance.Features.Evidence.Readers.EvidenceRequestReader>());
        var actor = new OperationsActor(fixture.ApproverUserId, fixture.ApproverMemberId,
            "Approver");

        // Act
        var read = await queue.ReadAsync(fixture.TenantId, fixture.ProgramId, actor, 30,
            CancellationToken.None);
        var proposerRead = await queue.ReadAsync(fixture.TenantId, fixture.ProgramId,
            new OperationsActor(fixture.LeadUserId, fixture.LeadMemberId, "Proposer"), 30,
            CancellationToken.None);

        // Assert
        Assert.True(read.IsSuccess);
        var entry = Assert.Single(read.Value.Entries,
            item => item.Candidate.Kind == WorkSource.ControlOperatingPlanApproval);
        Assert.True(entry.ActorEligible);
        Assert.Equal(plan.PlanVersionId, entry.Candidate.SourceId);
        Assert.Equal(fixture.ControlId, entry.Candidate.ControlId);
        Assert.Equal("Approve operating plan for AC-OPS", entry.Item.Summary);
        Assert.True(proposerRead.IsSuccess);
        var proposerEntry = Assert.Single(proposerRead.Value.Entries,
            item => item.Candidate.Kind == WorkSource.ControlOperatingPlanApproval);
        Assert.False(proposerEntry.ActorEligible);
    }

    static async Task<ControlEvaluationView> SubmitControlEvaluationAsync(OperationsFixture fixture)
    {
        var plan = await fixture.GetOrDefineEvaluationPlanAsync();
        var evaluation = await fixture.AsAsync(fixture.OwnerUserId, new StartControlEvaluation(Uuid.CreateVersion4(),
            fixture.TenantId, fixture.ProgramId, fixture.ControlId, plan.PlanVersionId));
        foreach (var step in evaluation.Steps)
            evaluation = await fixture.AsAsync(fixture.OwnerUserId, new RecordControlEvaluationStep(
                Uuid.CreateVersion4(), fixture.TenantId, fixture.ProgramId, fixture.ControlId, evaluation.EvaluationId,
                step.StepId, evaluation.Revision, ControlEvaluationStates.Met, "Inspected the exact items.",
                step.InspectedItems));
        return await fixture.AsAsync(fixture.OwnerUserId, new SubmitControlEvaluation(Uuid.CreateVersion4(),
            fixture.TenantId, fixture.ProgramId, fixture.ControlId, evaluation.EvaluationId, evaluation.Revision,
            [new("design", ControlEvaluationStates.Effective, "Design is effective."),
                new("implementation", ControlEvaluationStates.Effective, "Implementation is effective."),
                new("evidence_sufficiency", ControlEvaluationStates.Effective, "Evidence is sufficient.")]));
    }

    static async Task<(Uuid DecisionId, DateTimeOffset ProposedAt)>
        SeedCriterionApplicabilityProposalAsync(OperationsFixture fixture)
    {
        var editionId = Uuid.CreateVersion4();
        var proposedAt = DateTimeOffset.UtcNow;
        var decisionId = await MappingSeeds.ProposeNotApplicableAsync(fixture.Provider, fixture.TenantId,
            fixture.ProgramId, editionId, "AC-2.4", 0, "The organization has no in-scope system for this criterion.",
            fixture.OwnerMemberId, "Owner", proposedAt);
        return (decisionId, proposedAt);
    }

    static async Task<(Uuid MappingId, DateTimeOffset ProposedAt, Uuid EditionId)>
        SeedControlMappingProposalAsync(OperationsFixture fixture)
    {
        var editionId = Uuid.CreateVersion4();
        var proposedAt = DateTimeOffset.UtcNow;
        var mappingId = await MappingSeeds.ProposeMappingAsync(fixture.Provider, fixture.TenantId, fixture.ProgramId,
            fixture.ControlId, fixture.ControlVersionId, editionId, "AC-2.4", 0, "The control addresses this criterion.",
            "The control covers the in-scope environment.", fixture.OwnerMemberId, "Owner", proposedAt, "security");
        return (mappingId, proposedAt, editionId);
    }

    static async Task SeedEvidenceRequestAsync(OperationsFixture fixture)
    {
        var openedAt = DateTimeOffset.UtcNow;
        await EvidenceSeeds.OpenAsync(fixture.Provider, fixture.TenantId, fixture.ProgramId, Uuid.CreateVersion4(), "Access export", "Upload the approved access export.", fixture.OwnerMemberId, fixture.Today.AddDays(5), null, ActorReference.ForMember(fixture.LeadMemberId, "Lead"), openedAt);
    }

    static async Task<ProjectionCheckpoint> ReadCheckpointAsync(
        IDomainEventReader events, Uuid tenantId, string area)
    {
        var cursor = EventCursor.Start;
        await foreach (var record in events.ReadAsync(
                           EventStreamPattern.ForPattern(tenantId.ToString(), area),
                           cursor, CancellationToken.None))
            cursor = record.NextCursor;
        return new ProjectionCheckpoint(cursor);
    }

    sealed class ProjectedWorkItems(ProjectionCheckpoint checkpoint,
        IReadOnlyList<WorkCandidate>? candidates = null,
        string area = "evidence-requests", string kind = WorkSource.EvidenceRequest,
        string projectorName = "TestEvidenceWorkItems",
        IReadOnlyCollection<string>? kinds = null)
        : IAccountableWorkItemDirectoryReader
    {
        public string ProjectorName => projectorName;

        public IReadOnlyCollection<string> ProjectedKinds => kinds ?? [kind];

        public EventStreamPattern SourcePattern(Uuid tenantId) =>
            EventStreamPattern.ForPattern(tenantId.ToString(), area);

        public ProjectionCheckpoint Checkpoint { get; set; } = checkpoint;

        public ValueTask<ProjectionCheckpoint> LoadCheckpointAsync(Uuid tenantId,
            CancellationToken ct = default) => ValueTask.FromResult(Checkpoint);

        public ValueTask<Result<IReadOnlyList<WorkCandidate>>> LoadProgramAsync(Uuid tenantId,
            Uuid programId, CancellationToken ct = default) =>
            ValueTask.FromResult(Result<IReadOnlyList<WorkCandidate>>.Success(candidates ?? []));
    }
}
