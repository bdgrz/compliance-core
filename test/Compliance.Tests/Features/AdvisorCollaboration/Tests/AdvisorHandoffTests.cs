using System.Text.Json;
using Bdgrz.Compliance;
using Cntryl.Fitz.Testing;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.Boundaries.Rules;
using Bdgrz.Compliance.Features.Commitments.Rules;
using Bdgrz.Compliance.Features.Content.Persistence;
using Bdgrz.Compliance.Features.Controls.Rules;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.History;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Responsibilities.Models;
using Bdgrz.Compliance.Features.SourceFacts.Rules;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
using Bdgrz.Compliance.Tests.Features.Evidence.Tests;
using Bdgrz.Compliance.Tests.Features.Examinations.Tests;
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
using Bdgrz.Compliance.Tests.Features.Providers.Tests;
using Bdgrz.Compliance.Tests.Features.Readiness.Tests;
using Bdgrz.Compliance.Tests.Features.Remediation.Tests;
using Bdgrz.Compliance.Tests.Features.Risks.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Tests.Features.Workforce.Tests;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Aggregates;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Commands;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Events;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Models;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Queries;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Readers;
using Bdgrz.Compliance.Features.Controls.Aggregates;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Work.Commands;
namespace Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;

/// <summary>The handoff and thread state machines, their selection invariants and their serialized facts.</summary>
public sealed class AdvisorHandoffTests
{
    [Fact]
    public async Task ShouldCreateHandoffWithNativeNumericPolicyVersionGivenApprovedPolicy()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var selected = AdvisorRecordVersion.PolicyVersion(Uuid.CreateVersion4(), version: 2, revision: 5);
        var handoffId = Uuid.CreateVersion4();

        // Act
        var result = await host.CreateAsync(handoffId, [selected], "Review the approved policy");
        var saved = await host.SnapshotAsync(handoffId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal([selected], saved.Selection);
    }

    [Fact]
    public async Task ShouldSelectMultipleQuestionsFromOneBoundaryGivenExactBoundaryVersion()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var boundaryId = Uuid.CreateVersion4();
        var versionId = Uuid.CreateVersion4();
        var selected = new[]
        {
            AdvisorRecordVersion.BoundaryQuestion(boundaryId, Uuid.CreateVersion4(), versionId, revision: 3),
            AdvisorRecordVersion.BoundaryQuestion(boundaryId, Uuid.CreateVersion4(), versionId, revision: 3),
        };
        var handoffId = Uuid.CreateVersion4();

        // Act
        var result = await host.CreateAsync(handoffId, selected, "Review the open boundary questions");
        var saved = await host.SnapshotAsync(handoffId);

        // Assert
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(selected, saved.Selection);
    }

    [Fact]
    public async Task ShouldRejectUnboundedSelectionGivenMoreThanOneHundredExactSources()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var selected = Enumerable.Range(0, 101).Select(_ =>
            AdvisorRecordVersion.ControlDraft(Uuid.CreateVersion4(), Uuid.CreateVersion4(), 1)).ToArray();
        var handoffId = Uuid.CreateVersion4();

        // Act
        var result = await host.CreateAsync(handoffId, selected, "Review selected work");
        var saved = await host.HandoffAsync(handoffId);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
        Assert.False(saved.IsCreated);
        Assert.Empty(await host.EventsAsync<AdvisorHandoffCreated>(saved.Stream));
    }

    [Fact]
    public async Task ShouldRejectDuplicateLogicalSourceGivenSelection()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var recordId = Uuid.CreateVersion4();
        var handoffId = Uuid.CreateVersion4();

        // Act
        var result = await host.CreateAsync(handoffId,
        [
            new AdvisorRecordVersion(recordId, Uuid.CreateVersion4()),
            new AdvisorRecordVersion(recordId, Uuid.CreateVersion4()),
        ]);

        // Assert
        Assert.Equal(RequestErrorKind.Validation, result.Error?.Kind);
    }

    [Fact]
    public async Task ShouldRetainComparisonBaselineAndReadinessGivenReplayOfARepeatedHandoff()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (previousId, source) = await host.SeedReadyAsync();
        Assert.True((await host.EndAsync(previousId, 2, revoke: true)).IsSuccess);
        var handoffId = Uuid.CreateVersion4();
        var changed = new AdvisorRecordVersion(source.RecordId, Uuid.CreateVersion4());
        Assert.True((await host.CreateAsync(handoffId, [changed], "Review changed controls", previousId)).IsSuccess);
        Assert.True((await host.ReadyAsync(handoffId, 1)).IsSuccess);

        // Act
        var live = await host.HandoffAsync(handoffId);
        var replay = new AggregateScenario<AdvisorHandoff>(new AdvisorHandoff(host.TenantId, host.ProgramId, handoffId))
            .Given(live.Facts.ToArray()).Aggregate;

        // Assert
        Assert.Equal(previousId, live.ComparedToHandoffId);
        Assert.True(live.WasReadyForReview);
        Assert.Equal(previousId, replay.ComparedToHandoffId);
        Assert.True(replay.WasReadyForReview);
        Assert.Equal(live.SelectionManifest, replay.SelectionManifest);
        Assert.Equal(live.Revision, replay.Revision);
    }

    [Fact]
    public async Task ShouldRetainValidationAsHistoricalGivenASelectedRecordAdvancesToANewVersion()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, source) = await host.SeedReadyAsync();
        var feedbackId = Uuid.CreateVersion4();
        Assert.True((await host.RecordAsync(host.Feedback(handoffId, feedbackId, source, 2,
            "Reviewed against the evidence", "validated"))).IsSuccess);
        await host.MaterializeAsync(handoffId);
        var successor = new AdvisorRecordVersion(source.RecordId, Uuid.CreateVersion4());
        host.Resolver.Checked.Clear();

        // Act
        var listedBefore = await host.ListFeedbackHandler.HandleAsync(AdvisorHost.System(new ListAdvisorFeedback(
            host.TenantId, host.ProgramId, handoffId)), CancellationToken.None);
        Assert.True((await host.ReviseAsync(handoffId, 3, [successor], "The control draft was revised")).IsSuccess);
        var listedAfter = await host.ListFeedbackHandler.HandleAsync(AdvisorHost.System(new ListAdvisorFeedback(
            host.TenantId, host.ProgramId, handoffId)), CancellationToken.None);
        var thread = await host.ThreadAsync(handoffId, feedbackId);

        // Assert
        Assert.Equal(source.VersionId, Assert.Single(listedAfter.Value.Items).RecordVersionId);
        Assert.False(Assert.Single(listedAfter.Value.Items).IsCurrent);
        Assert.Equal("validated", thread.Recorded!.Kind);
        Assert.Equal(source.VersionId, thread.Recorded.RecordVersionId);
        Assert.Single(listedBefore.Value.Items);
    }

    [Fact]
    public async Task ShouldKeepNativeSelectedSourceGivenSerializedFeedbackFact()
    {
        // Arrange
        var selected = AdvisorRecordVersion.PolicyVersion(Uuid.CreateVersion4(), 3, 7);
        var tenantId = Uuid.CreateVersion4();
        var programId = Uuid.CreateVersion4();
        var handoffId = Uuid.CreateVersion4();
        var content = new FitzKvImmutableContentStore(new InMemoryKvClient());
        var recorded = (await AdvisorEvents.Recorded(content, tenantId, programId, handoffId, Uuid.CreateVersion4(), selected,
            "change_request", "Clarify this policy requirement.", ActorReference.ForMember(Uuid.CreateVersion4(),
                "Advisor"), AdvisorHost.Start)) with
        {
            SelectedSource = selected,
        };

        // Act
        var json = JsonSerializer.Serialize(recorded, ComplianceCoreJsonContext.Default.AdvisorFeedbackRecorded);
        var replay = JsonSerializer.Deserialize(json, ComplianceCoreJsonContext.Default.AdvisorFeedbackRecorded);

        // Assert
        Assert.Equal(selected, replay!.SelectedSource);
        Assert.Equal(selected.RecordId, replay.RecordId);
        Assert.Equal(Uuid.Empty, replay.RecordVersionId);
        Assert.Equal(recorded.Operation, replay.Operation);
        Assert.Equal(recorded.Intent, replay.Intent);
    }

    [Fact]
    public void ShouldRoundTripAdmissionFactGivenSerializedHandoffStream()
    {
        // Arrange
        var admitted = AdvisorEvents.Admitted(Uuid.CreateVersion4(), Uuid.CreateVersion4(), Uuid.CreateVersion4(), 3,
            2, ActorReference.ForMember(Uuid.CreateVersion4(), "Advisor"), AdvisorHost.Start);

        // Act
        var json = JsonSerializer.Serialize(admitted, ComplianceCoreJsonContext.Default.AdvisorFeedbackAdmitted);
        var replay = JsonSerializer.Deserialize(json, ComplianceCoreJsonContext.Default.AdvisorFeedbackAdmitted);

        // Assert
        Assert.Equal(admitted.Owner, replay!.Owner);
        Assert.Equal(3, replay.Revision);
        Assert.Equal(2, replay.Generation);
        Assert.Equal(admitted.Intent, replay.Intent);
        Assert.Equal(admitted.Operation, replay.Operation);
    }

    [Fact]
    public async Task ShouldReplayThreadGivenItsFactsAndNotLoadTheHandoff()
    {
        // Arrange
        await using var host = new AdvisorHost();
        var (handoffId, feedbackId, _) = await host.SeedFeedbackAsync();
        Assert.True((await host.CorrectHandler.HandleAsync(host.Http(new CorrectAdvisorFeedback(host.TenantId,
            host.ProgramId, handoffId, feedbackId, Uuid.CreateVersion4(), "Clarified.", "Ambiguous.", 1)),
            CancellationToken.None)).IsSuccess);
        Assert.True((await host.RespondHandler.HandleAsync(host.Http(new RespondToAdvisorFeedback(
            Uuid.CreateVersion4(), host.TenantId, host.ProgramId, handoffId, feedbackId, "Updated.", 2)),
            CancellationToken.None)).IsSuccess);
        Assert.True((await host.ResolveHandler.HandleAsync(host.Http(new ResolveAdvisorFeedback(
            Uuid.CreateVersion4(), host.TenantId, host.ProgramId, handoffId, feedbackId, 3)),
            CancellationToken.None)).IsSuccess);

        // Act
        var live = await host.ThreadAsync(handoffId, feedbackId);
        var replay = new AggregateScenario<AdvisorFeedbackThread>(new AdvisorFeedbackThread(host.TenantId,
            host.ProgramId, handoffId, feedbackId)).Given(live.Facts.ToArray()).Aggregate;

        // Assert
        Assert.Equal(4, live.Revision);
        Assert.Equal(live.Revision, replay.Revision);
        Assert.Equal(live.Recorded!.Fact, replay.Recorded);
        Assert.Equal(live.Resolved, replay.Resolved);
        Assert.Equal("Please clarify the control owner.", (await AdvisorFeedbackReader.ReadRecordedAsync(host.Content,
            replay.Recorded!, CancellationToken.None)).Body);
    }
}
