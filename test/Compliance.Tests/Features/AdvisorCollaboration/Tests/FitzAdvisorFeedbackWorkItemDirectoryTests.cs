using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Fitz.Testing;
using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Readers;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Services;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Work.Models;
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
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Aggregates;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Events;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;

public sealed class FitzAdvisorFeedbackWorkItemDirectoryTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    static readonly ActorReference Manager = ActorReference.ForMember(Uuid.CreateVersion4(), "Compliance Lead");

    static readonly ActorReference Author = ActorReference.ForMember(Uuid.CreateVersion4(), "Assigned Advisor");

    static async Task<AdvisorFeedbackRecorded> Recorded(IImmutableContentStore content, Uuid tenantId,
        Uuid programId, Uuid handoffId, Uuid feedbackId, AdvisorRecordVersion source, string kind, string body,
        ulong version = 1) => WithMetadata(
        await AdvisorEvents.Recorded(content, tenantId, programId, handoffId, feedbackId, source, kind, body, Author, Now.AddMinutes(2)),
        tenantId, programId, handoffId, feedbackId, version);

    [Fact]
    public async Task ShouldMoveAttributedFeedbackWorkFromResponseToResolutionGivenOrderedEvents()
    {
        // Arrange
        var content = new FitzKvImmutableContentStore(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var handoffId = Uuid.CreateVersion4();
        var feedbackId = Uuid.CreateVersion4();
        var source = new AdvisorRecordVersion(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var feedback = await Recorded(content, tenantId, programId, handoffId, feedbackId, source, "change_request",
            "Clarify ownership.");
        var directory = new FitzAdvisorFeedbackWorkItemDirectory(new InMemoryKvClient(), content);

        // Act
        await ApplyAsync(directory, tenantId, feedback);
        var recorded = await directory.LoadProgramAsync(tenantId, programId);
        var respondedEvent = WithMetadata(await AdvisorEvents.Responded(content, tenantId, programId, handoffId, feedbackId,
            "We updated the control draft.", Manager, Now.AddMinutes(3)), tenantId, programId, handoffId, feedbackId, 2);
        await ApplyAsync(directory, tenantId, respondedEvent);
        var responded = await directory.LoadProgramAsync(tenantId, programId);
        var resolvedEvent = WithMetadata(AdvisorEvents.Resolved(tenantId, programId, handoffId, feedbackId, Manager,
            Now.AddMinutes(4)), tenantId, programId, handoffId, feedbackId, 3);
        await ApplyAsync(directory, tenantId, resolvedEvent);
        var resolved = await directory.LoadProgramAsync(tenantId, programId);

        // Assert
        var candidate = Assert.Single(recorded.Value);
        Assert.Equal(WorkSource.AdvisorFeedback, candidate.Kind);
        Assert.Equal(WorkCandidate.IdFor(AdvisorFeedbackThread.IdentityOf(tenantId, programId, handoffId, feedbackId),
            WorkSource.AdvisorFeedback), candidate.WorkItemId);
        Assert.Equal(feedbackId, candidate.SourceId);
        Assert.Equal("Assigned Advisor: Clarify ownership.", candidate.Reason);
        Assert.Equal(new OperatingHolder(OperatingAuthority.ProgramManagerHolder, programId), candidate.Responsible);
        Assert.Equal("respond_to_feedback", candidate.NextAction);
        Assert.Contains($"/feedback/{feedbackId}/responses", candidate.ActionPath, StringComparison.Ordinal);
        var responseCandidate = Assert.Single(responded.Value);
        Assert.Equal("resolve_feedback", responseCandidate.NextAction);
        Assert.Contains($"/feedback/{feedbackId}/resolutions", responseCandidate.ActionPath, StringComparison.Ordinal);
        Assert.Empty(resolved.Value);
    }

    [Fact]
    public async Task ShouldExcludeValidationFromActionableWorkGivenFeedbackKinds()
    {
        // Arrange
        var content = new FitzKvImmutableContentStore(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var handoffId = Uuid.CreateVersion4();
        var source = new AdvisorRecordVersion(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var directory = new FitzAdvisorFeedbackWorkItemDirectory(new InMemoryKvClient(), content);

        // Act
        await ApplyAsync(directory, tenantId,
            await Recorded(content, tenantId, programId, handoffId, Uuid.CreateVersion4(), source, "comment", "Clarify ownership."),
            await Recorded(content, tenantId, programId, handoffId, Uuid.CreateVersion4(), source, "validated",
                "The control is well designed."));
        var page = await directory.LoadProgramAsync(tenantId, programId);

        // Assert
        var candidate = Assert.Single(page.Value);
        Assert.Equal("advisor_feedback", candidate.Kind);
        Assert.Contains("Clarify ownership.", candidate.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldRetainCorrectedActionableFeedbackGivenLaterCorrection()
    {
        // Arrange
        var content = new FitzKvImmutableContentStore(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var handoffId = Uuid.CreateVersion4();
        var feedbackId = Uuid.CreateVersion4();
        var source = new AdvisorRecordVersion(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var directory = new FitzAdvisorFeedbackWorkItemDirectory(new InMemoryKvClient(), content);

        // Act
        await ApplyAsync(directory, tenantId,
            await Recorded(content, tenantId, programId, handoffId, feedbackId, source, "change_request", "Clarify ownership."),
            WithMetadata(await AdvisorEvents.Corrected(content, tenantId, programId, handoffId, feedbackId,
                "Clarify the control owner.", "The first wording was unclear.", Author, Now.AddMinutes(3)),
                tenantId, programId, handoffId, feedbackId, 2));
        var work = await directory.LoadProgramAsync(tenantId, programId);

        // Assert
        var candidate = Assert.Single(work.Value);
        Assert.Contains("Clarify the control owner.", candidate.Reason, StringComparison.Ordinal);
        Assert.Equal("respond_to_feedback", candidate.NextAction);
    }

    [Fact]
    public async Task ShouldIgnoreExactReplayAndRejectConflictingReplayGivenProjectedFeedback()
    {
        // Arrange
        var content = new FitzKvImmutableContentStore(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var handoffId = Uuid.CreateVersion4();
        var feedbackId = Uuid.CreateVersion4();
        var source = new AdvisorRecordVersion(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var directory = new FitzAdvisorFeedbackWorkItemDirectory(new InMemoryKvClient(), content);
        var feedback = await Recorded(content, tenantId, programId, handoffId, feedbackId, source, "comment", "Clarify ownership.");
        await ApplyAsync(directory, tenantId, feedback);

        // Act
        await ApplyAsync(directory, tenantId, feedback);
        var conflictingReplay = await Recorded(content, tenantId, programId, handoffId, feedbackId, source, "comment",
            "Different event at the same revision.");
        await Assert.ThrowsAsync<InvalidOperationException>(() => ApplyAsync(directory, tenantId, conflictingReplay));
        var work = await directory.LoadProgramAsync(tenantId, programId);

        // Assert
        var candidate = Assert.Single(work.Value);
        Assert.Contains("Clarify ownership.", candidate.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("Different event", candidate.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShouldRejectForeignTenantEventGivenTenantScopedProjectionBatch()
    {
        // Arrange
        var content = new FitzKvImmutableContentStore(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var foreignTenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var handoffId = Uuid.CreateVersion4();
        var feedbackId = Uuid.CreateVersion4();
        var source = new AdvisorRecordVersion(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var foreign = await Recorded(content, foreignTenantId, programId, handoffId, feedbackId, source, "comment", "Foreign.");
        var directory = new FitzAdvisorFeedbackWorkItemDirectory(new InMemoryKvClient(), content);
        var identity = new CheckpointIdentity(FitzAdvisorFeedbackWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), AdvisorFeedbackThread.Area));

        // Act
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => directory.ApplyAsync(foreign).AsTask());
    }

    [Fact]
    public async Task ShouldRejectFollowUpFromAnotherProgramGivenThreadProjectionState()
    {
        // Arrange
        var content = new FitzKvImmutableContentStore(new InMemoryKvClient());
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var otherProgramId = Uuid.CreateVersion4();
        var handoffId = Uuid.CreateVersion4();
        var feedbackId = Uuid.CreateVersion4();
        var source = new AdvisorRecordVersion(Uuid.CreateVersion4(), Uuid.CreateVersion4());
        var directory = new FitzAdvisorFeedbackWorkItemDirectory(new InMemoryKvClient(), content);
        await ApplyAsync(directory, tenantId,
            await Recorded(content, tenantId, programId, handoffId, feedbackId, source, "change_request", "Clarify ownership."));
        var identity = new CheckpointIdentity(FitzAdvisorFeedbackWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), AdvisorFeedbackThread.Area));
        var foreignFollowUp = WithMetadata(await AdvisorEvents.Responded(content, tenantId, otherProgramId, handoffId, feedbackId,
            "Update.", Manager, Now.AddMinutes(3)), tenantId, programId, handoffId, feedbackId, 2);

        // Act
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => directory.ApplyAsync(foreignFollowUp).AsTask());
    }

    static async Task ApplyAsync(FitzAdvisorFeedbackWorkItemDirectory directory, Uuid tenantId,
        params DomainEvent[] events)
    {
        var identity = new CheckpointIdentity(FitzAdvisorFeedbackWorkItemDirectory.ProjectorName,
            EventStreamPattern.ForPattern(tenantId.ToString(), AdvisorFeedbackThread.Area));
        await using var batch = await directory.BeginAsync(new ProjectionBatchContext(identity,
            ProjectionCheckpoint.Start));
        foreach (var domainEvent in events)
            await directory.ApplyAsync(domainEvent);
        await batch.CommitAsync(ProjectionCheckpoint.Start);
    }

    static T WithMetadata<T>(T domainEvent, Uuid tenantId, Uuid programId, Uuid handoffId, Uuid feedbackId,
        ulong version) where T : DomainEvent
    {
        domainEvent.AttachMetadata(new DomainEventMetadata(Uuid.CreateVersion4(),
            AdvisorFeedbackThread.IdentityOf(tenantId, programId, handoffId, feedbackId), version, Now));
        return domainEvent;
    }
}
