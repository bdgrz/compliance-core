using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Microsoft.Extensions.DependencyInjection;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Readers;
using Bdgrz.Compliance.Features.AdvisorCollaboration.Processes;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.AuditPopulations.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Models;
using Bdgrz.Compliance.Features.AuditorRequests.Processes;
using Bdgrz.Compliance.Features.Content.Contracts;
using Bdgrz.Compliance.Features.ControlMappings.History;
using Bdgrz.Compliance.Features.Controls.Contracts;
using Bdgrz.Compliance.Features.Evaluations.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.NamedRecords.Processes;
using Bdgrz.Compliance.Features.NativeOperations.History;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Operations.History;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Processes;
using Bdgrz.Compliance.Features.PolicyDistribution.Rules;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Readiness.Models;
using Bdgrz.Compliance.Features.Readiness.Rules;
using Bdgrz.Compliance.Features.Remediation.Models;
using Bdgrz.Compliance.Features.Remediation.Processes;
using Bdgrz.Compliance.Features.Retention.Models;
using Bdgrz.Compliance.Features.SourceFacts.Models;
using Bdgrz.Compliance.Features.SystemDescription.Projections;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.AdvisorCollaboration.Tests;
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
using Bdgrz.Compliance.Tests.Features.NamedRecords.Tests;
using Bdgrz.Compliance.Tests.Features.NativeOperations.Tests;
using Bdgrz.Compliance.Tests.Features.Operations.Tests;
using Bdgrz.Compliance.Tests.Features.Policies.Tests;
using Bdgrz.Compliance.Tests.Features.Processes.Tests;
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
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.Examinations.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.PolicyDistribution.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Reactors;
using Bdgrz.Compliance.Features.Processes.Models;
using Bdgrz.Compliance.Features.Providers.Aggregates;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
using Bdgrz.Compliance.Features.Tenants.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.PolicyDistribution.Tests;

public sealed class PolicyCampaignClosureProcessTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task ShouldSettleHeldUsesGivenCancellationPersistedBeforeProcessCheckpoint(int stageIndex)
    {
        // Arrange
        var crash = new CrashAfterReplaces();
        await using var host = new CampaignHost(crash.Wrap);
        await host.SeedAsync();
        var campaign = await host.LaunchAsync();
        var (ackId, closeId) = await AcknowledgeAndCloseAsync(host, campaign);
        await host.ReactResultsAsync(campaign);
        await host.RunAsync(await host.ResultKey(ackId));
        crash.ArmAfterReplaceCount(1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => host.ReactClosureAsync(campaign));
        crash.Disarm();
        var key = await host.ClosureKey(closeId);
        var record = (await host.RecordAsync(key))!;
        Assert.True((await host.ParticipantAsync(campaign, host.LearnerPerson)).Uses.HasActiveUse);
        record = await host.Records.ReplaceAsync(record with { StageIndex = stageIndex, Lease = null }, record.Version);
        var abort = (await host.StepAsync(record, "abort", nameof(AbortPolicyCampaignClosure)))!;
        Assert.IsType<ReceiverOutcome.Applied>((await host.DispatchAsync(abort)).Value);

        // Act: recover after the cancellation append, before its participant settlements or the driver's checkpoint.
        var resumed = await host.RunAsync(key);

        // Assert
        Assert.False((await host.ParticipantAsync(campaign, host.LearnerPerson)).Uses.HasActiveUse);
        Assert.Equal(ProcessState.Aborted, resumed.State);
        Assert.Null((await host.CampaignAsync(campaign)).Closing);
    }

    [Fact]
    public async Task ShouldCloseWithExactTotalsAndSettleEveryUseGivenAppliedResults()
    {
        // Arrange
        await using var host = new CampaignHost();
        await host.SeedAsync();
        var campaign = await host.LaunchAsync();
        var (ackId, closeId) = await AcknowledgeAndCloseAsync(host, campaign);
        await host.ReactResultsAsync(campaign);
        var applied = await host.RunAsync(await host.ResultKey(ackId));
        await host.ReactClosureAsync(campaign);

        // Act
        var closed = await host.RunAsync(await host.ClosureKey(closeId));

        // Assert
        Assert.Equal(ProcessState.Completed, applied.State);
        Assert.Equal(ProcessState.Completed, closed.State);
        var owner = await host.CampaignAsync(campaign);
        var fact = Assert.IsType<PolicyCampaignClosed>(owner.Closing);
        Assert.Equal(closeId, fact.Operation.RootOperationId);
        Assert.Equal(0, fact.AudienceGeneration);
        Assert.Equal(4, owner.Revision);
        var state = await host.StateAsync(campaign);
        var view = state.ToView(host.Today);
        Assert.Equal("closed", view.Status);
        Assert.Equal(new CampaignTotalsView(2, 2, 0, 0, 1, 0, 1, 0), view.ClosingTotals);
        var key = PolicyCampaignClosureEffects.UseKey(closeId, owner.Stream, 1,
            new PolicyCampaignParticipant(host.Tenant, campaign, host.LearnerPerson).Stream);
        var participant = await host.ParticipantAsync(campaign, host.LearnerPerson, key);
        Assert.NotNull(participant.Acknowledgement);
        Assert.True(participant.Uses.Find(key)!.IsSettled);
        Assert.Empty(participant.Uses.ActiveUses);
        Assert.Empty(await host.EventsAsync<PolicyCampaignParticipantCampaignClosureUseAuthorized>(
            (await host.ParticipantAsync(campaign, host.OfflinePerson)).Stream));
    }

    [Fact]
    public async Task ShouldWaitForAdmittedResultToBeAppliedGivenClosureRacingMaterialization()
    {
        // Arrange
        await using var host = new CampaignHost();
        await host.SeedAsync();
        var campaign = await host.LaunchAsync();
        var (ackId, closeId) = await AcknowledgeAndCloseAsync(host, campaign);
        await host.ReactClosureAsync(campaign);

        // Act: the result is admitted but not applied, so the closure cannot hold the participant.
        var waiting = await host.RunAsync(await host.ClosureKey(closeId));
        var stillOpen = await host.CampaignAsync(campaign);
        await host.ReactResultsAsync(campaign);
        await host.RunAsync(await host.ResultKey(ackId));
        host.Clock.Advance(ProcessDriver.RetryDelay + TimeSpan.FromSeconds(1));
        var closed = await host.RunAsync(await host.ClosureKey(closeId));

        // Assert
        Assert.Equal(ProcessState.Pending, waiting.State);
        Assert.Contains("not applied", waiting.Note, StringComparison.Ordinal);
        Assert.Null(stillOpen.Closing);
        Assert.Equal(ProcessState.Completed, closed.State);
        Assert.Equal(1, (await host.StateAsync(campaign)).ToView(host.Today).ClosingTotals!.Satisfied);
    }

    [Fact]
    public async Task ShouldAdmitExactlyOneGivenAcknowledgementRacingCloseAtSameRevision()
    {
        // Arrange
        await using var host = new CampaignHost();
        await host.SeedAsync();
        var campaign = await host.LaunchAsync();
        var acknowledge = host.Acknowledge(campaign, host.LearnerPerson, 1);
        var close = new CloseCampaign(Uuid.CreateVersion4(), host.Tenant, host.Program, campaign, "Done", 1);

        // Act
        var ack = host.SendAsync(acknowledge, host.Learner);
        var closing = host.SendAsync(close, host.Manager);
        var results = await Task.WhenAll(ack, closing);
        var owner = await host.CampaignAsync(campaign);

        // Assert
        Assert.Equal(1, results.Count(result => result.IsSuccess));
        Assert.Equal(2, owner.Revision);
        Assert.Equal(1, owner.Admitted.Count + (owner.PendingClosure is null ? 0 : 1));
        var loser = results.Single(result => !result.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, loser.Error!.Kind);
    }

    [Fact]
    public async Task ShouldRejectStaleCloseAndLateAcknowledgementGivenAcknowledgedCampaign()
    {
        // Arrange
        await using var host = new CampaignHost();
        await host.SeedAsync();
        var campaign = await host.LaunchAsync();
        var acknowledged = await host.SendAsync(host.Acknowledge(campaign, host.LearnerPerson, 1), host.Learner);

        // Act
        var stale = await host.SendAsync(new CloseCampaign(Uuid.CreateVersion4(), host.Tenant, host.Program, campaign,
            "Done", 1), host.Manager);
        var closed = await host.SendAsync(new CloseCampaign(Uuid.CreateVersion4(), host.Tenant, host.Program, campaign,
            "Done", 2), host.Manager);
        var late = await host.SendAsync(host.Acknowledge(campaign, host.OfflinePerson, 3), host.Manager);

        // Assert
        Assert.True(acknowledged.IsSuccess, acknowledged.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, stale.Error?.Kind);
        Assert.True(closed.IsSuccess, closed.Error?.Message);
        Assert.Equal(RequestErrorKind.Conflict, late.Error?.Kind);
        var owner = await host.CampaignAsync(campaign);
        Assert.Single(owner.Admitted);
        Assert.NotNull(owner.PendingClosure);
    }

    [Fact]
    public async Task ShouldReturnOriginalAcceptanceGivenExactCloseRetryAfterClosureCompleted()
    {
        // Arrange
        await using var host = new CampaignHost();
        await host.SeedAsync();
        var campaign = await host.LaunchAsync();
        var close = new CloseCampaign(Uuid.CreateVersion4(), host.Tenant, host.Program, campaign, "Done", 1);
        var first = await host.SendAsync(close, host.Manager);
        await host.ReactClosureAsync(campaign);
        await host.RunAsync(await host.ClosureKey(close.CommandId));
        var before = (await host.CampaignAsync(campaign)).CommittedStreamPosition;

        // Act
        var retry = await host.SendAsync(close, host.Manager);
        var changed = await host.SendAsync(close with { Rationale = "A different rationale." }, host.Manager);

        // Assert
        Assert.True(first.IsSuccess, first.Error?.Message);
        Assert.Equal(first.Value, retry.Value);
        Assert.Equal(RequestErrorKind.Conflict, changed.Error?.Kind);
        var owner = await host.CampaignAsync(campaign);
        Assert.Equal(before, owner.CommittedStreamPosition);
        Assert.NotNull(owner.Closing);
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public async Task ShouldResumeWithIdenticalIdsAndNoDuplicateFactsGivenCrashAfterSomeReceivers(int replaces,
        bool closedBeforeCrash)
    {
        // Arrange
        var crash = new CrashAfterReplaces();
        await using var host = new CampaignHost(crash.Wrap);
        await host.SeedAsync();
        var campaign = await host.LaunchAsync();
        var (ackId, closeId) = await AcknowledgeAndCloseAsync(host, campaign);
        await host.ReactResultsAsync(campaign);
        await host.RunAsync(await host.ResultKey(ackId));
        crash.ArmAfterReplaceCount(replaces);

        // Act
        var crashed = await Record.ExceptionAsync(() => host.ReactClosureAsync(campaign));
        var interrupted = await host.RecordAsync(await host.ClosureKey(closeId));
        var partial = (await host.CampaignAsync(campaign)).Closing;
        crash.Disarm();
        var resumed = await host.RunAsync(await host.ClosureKey(closeId), $"reactor:{PolicyCampaignClosureReactor.WorkloadName}");
        var replayed = await host.RunAsync(await host.ClosureKey(closeId));
        var owner = await host.CampaignAsync(campaign);

        // Assert
        Assert.IsType<InvalidOperationException>(crashed);
        Assert.NotEqual(ProcessState.Completed, interrupted!.State);
        Assert.Equal(closedBeforeCrash, partial is not null);
        Assert.Equal(ProcessState.Completed, resumed.State);
        Assert.Equal(ProcessState.Completed, replayed.State);
        Assert.NotNull(owner.Closing);
        var participant = await host.ParticipantAsync(campaign, host.LearnerPerson);
        Assert.Equal(1UL, participant.Uses.Generation);
        Assert.Single(await host.EventsAsync<PolicyCampaignClosed>(owner.Stream));
        Assert.Single(await host.EventsAsync<PolicyCampaignParticipantCampaignClosureUseAuthorized>(participant.Stream));
        Assert.Single(await host.EventsAsync<PolicyCampaignParticipantCampaignClosureUseSettled>(participant.Stream));
    }

    [Fact]
    public async Task ShouldCancelClosureAndFenceFinalizationGivenParticipantDivergingFromFrozenResults()
    {
        // Arrange
        await using var host = new CampaignHost();
        await host.SeedAsync();
        var campaign = await host.LaunchAsync();
        var (ackId, closeId) = await AcknowledgeAndCloseAsync(host, campaign);
        await host.ReactResultsAsync(campaign);
        await host.RunAsync(await host.ResultKey(ackId));
        // A foreign result lands on the participant after the closure froze its results: a permanent mismatch.
        await AppendForeignResultAsync(host, campaign, host.LearnerPerson);
        await host.ReactClosureAsync(campaign);

        // Act
        var aborted = await host.RunAsync(await host.ClosureKey(closeId));
        var owner = await host.CampaignAsync(campaign);
        var key = PolicyCampaignClosureEffects.UseKey(closeId, owner.Stream, 1,
            new PolicyCampaignParticipant(host.Tenant, campaign, host.LearnerPerson).Stream);
        var participant = await host.ParticipantAsync(campaign, host.LearnerPerson, key);

        // Assert
        Assert.Equal(ProcessState.Completed, aborted.State);
        Assert.Null(owner.Closing);
        Assert.Null(owner.PendingClosure);
        Assert.NotNull(owner.CanceledFor(closeId));
        Assert.Equal(2, owner.Generation);
        var fence = participant.Uses.Find(key)!;
        Assert.True(fence.IsFence);
        Assert.Empty(await host.EventsAsync<PolicyCampaignParticipantCampaignClosureUseAuthorized>(
            (await host.ParticipantAsync(campaign, host.OfflinePerson)).Stream));
    }

    [Fact]
    public async Task ShouldRefuseDelayedAcquisitionAndFinalizationGivenSettlementOrCancellationRecorded()
    {
        // Arrange
        await using var host = new CampaignHost();
        await host.SeedAsync();
        var campaign = await host.LaunchAsync();
        var (ackId, closeId) = await AcknowledgeAndCloseAsync(host, campaign);
        await host.ReactResultsAsync(campaign);
        await host.RunAsync(await host.ResultKey(ackId));
        await AppendForeignResultAsync(host, campaign, host.LearnerPerson);
        await host.ReactClosureAsync(campaign);
        var record = (await host.RecordAsync(await host.ClosureKey(closeId)))!;
        var acquire = (await host.StepAsync(record, "acquire", nameof(AuthorizePolicyCampaignParticipantCampaignClosureUse),
            record.Targets[0]))!;
        var complete = (await host.StepAsync(record, "complete", nameof(CompletePolicyCampaignClosure)))!;
        await host.RunAsync(await host.ClosureKey(closeId));

        // Act
        var delayedAcquire = await host.DispatchAsync(acquire);
        var delayedComplete = await host.DispatchAsync(complete);

        // Assert
        var superseded = Assert.IsType<ReceiverOutcome.Rejected>(delayedAcquire.Value);
        Assert.Contains("canceled", superseded.Reason, StringComparison.Ordinal);
        var finalization = Assert.IsType<ReceiverOutcome.Rejected>(delayedComplete.Value);
        Assert.Contains("canceled", finalization.Reason, StringComparison.Ordinal);
        Assert.Null((await host.CampaignAsync(campaign)).Closing);
        Assert.Empty(await host.EventsAsync<PolicyCampaignParticipantCampaignClosureUseAuthorized>(
            (await host.ParticipantAsync(campaign, host.LearnerPerson)).Stream));
    }

    [Fact]
    public async Task ShouldRejectWithoutAppendGivenWrongOrForgedSourceProof()
    {
        // Arrange
        await using var host = new CampaignHost();
        await host.SeedAsync();
        var campaign = await host.LaunchAsync();
        var (ackId, closeId) = await AcknowledgeAndCloseAsync(host, campaign);
        await host.ReactResultsAsync(campaign);
        await host.RunAsync(await host.ResultKey(ackId));
        await host.ReactClosureAsync(campaign);
        var record = (await host.RecordAsync(await host.ClosureKey(closeId)))!;
        var acquire = (AuthorizePolicyCampaignParticipantCampaignClosureUse)(await host.StepAsync(record, "acquire",
            nameof(AuthorizePolicyCampaignParticipantCampaignClosureUse), record.Targets[0]))!;
        var complete = (CompletePolicyCampaignClosure)(await host.StepAsync(record, "complete",
            nameof(CompletePolicyCampaignClosure)))!;
        var owner = await host.CampaignAsync(campaign);
        var participant = await host.ParticipantAsync(campaign, host.LearnerPerson);
        var forged = record.Source with { EventId = Uuid.CreateVersion4() };

        // Act
        var wrongSource = await host.DispatchAsync(acquire with { Source = forged });
        var wrongTarget = await host.DispatchAsync(acquire with { Target = new EventStreamAddress(participant.Stream.Realm, participant.Stream.Area, "other") });
        var wrongGeneration = await host.DispatchAsync(complete with { Generation = 2 });
        var wrongIntent = await host.DispatchAsync(complete with { Intent = complete.ParticipantProofManifest });
        var forgedComplete = await host.DispatchAsync(complete with { Source = forged });

        // Assert
        foreach (var result in new[] { wrongSource, wrongTarget, wrongGeneration, wrongIntent, forgedComplete })
            Assert.False(result.IsSuccess && result.Value is not ReceiverOutcome.Superseded, result.Error?.Message);
        Assert.Equal(owner.CommittedStreamPosition, (await host.CampaignAsync(campaign)).CommittedStreamPosition);
        Assert.Equal(participant.CommittedStreamPosition,
            (await host.ParticipantAsync(campaign, host.LearnerPerson)).CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldRejectReceiversGivenNonSystemCaller()
    {
        // Arrange
        await using var host = new CampaignHost();
        await host.SeedAsync();
        var campaign = await host.LaunchAsync();
        var (_, closeId) = await AcknowledgeAndCloseAsync(host, campaign);
        await host.ReactClosureAsync(campaign);
        var record = (await host.RecordAsync(await host.ClosureKey(closeId)))!;
        var complete = (CompletePolicyCampaignClosure)(await host.StepAsync(record, "complete",
            nameof(CompletePolicyCampaignClosure)))!;

        // Act
        var forbidden = await host.SendAsync(complete, host.Manager);

        // Assert
        Assert.Equal(RequestErrorKind.Forbidden, forbidden.Error?.Kind);
    }

    static async Task<(Uuid AckId, Uuid CloseId)> AcknowledgeAndCloseAsync(CampaignHost host, Uuid campaign)
    {
        var acknowledge = host.Acknowledge(campaign, host.LearnerPerson, 1);
        var acknowledged = await host.SendAsync(acknowledge, host.Learner);
        Assert.True(acknowledged.IsSuccess, acknowledged.Error?.Message);
        var close = new CloseCampaign(Uuid.CreateVersion4(), host.Tenant, host.Program, campaign, "Done", 2);
        var closing = await host.SendAsync(close, host.Manager);
        Assert.True(closing.IsSuccess, closing.Error?.Message);
        return (acknowledge.CommandId, close.CommandId);
    }

    /// <summary>Appends a result to the participant outside any admission, as a corrupted or forged writer would.</summary>
    static async Task AppendForeignResultAsync(CampaignHost host, Uuid campaign, Uuid person)
    {
        await using var scope = host.Provider.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IAggregateReader>();
        var content = scope.ServiceProvider.GetRequiredService<IImmutableContentStore>();
        var owner = await reader.HydrateAsync(new PolicyCampaignParticipant(host.Tenant, campaign, person));
        var parent = await host.CampaignAsync(campaign);
        var root = Uuid.CreateVersion4();
        var at = host.Clock.GetUtcNow();
        var request = new ApproveCampaignWaiver(root, host.Tenant, host.Program, campaign, person, "Forged",
            host.Today.AddMonths(1), parent.Revision);
        var authored = await PolicyCampaignContent.WriteAsync(content, host.Tenant, PolicyCampaignContent.WaiverCommandSchema,
            request, CancellationToken.None);
        var proof = await PolicyCampaignContent.WriteManifestAsync(content, host.Tenant,
            PolicyCampaignContent.AdmissionProofSchema, [new PolicyParticipantAdmissionProof(
                NativeFactReference.Of(parent.Stream, parent.Launch!), NativeFactReference.Of(parent.Stream, parent.Frozen!),
                null, false)], SelectionOrder.CanonicalKey, CancellationToken.None);
        var rootProvenance = PolicyCampaignContent.Provenance(root, nameof(ApproveCampaignWaiver), parent.Stream,
            owner.Stream, host.ManagerActor, parent.Revision, AuthoredIntentDigest.Compute(request), at);
        var header = PolicyCampaignContent.HeaderOf(rootProvenance, parent.Stream, owner.Stream, at);
        var intent = await PolicyCampaignContent.WriteAsync(content, host.Tenant, ChildAdmissionIntent.Schema,
            new ChildAdmissionIntent(header, campaign, (ulong)parent.Generation, AdmittedChildKind.CampaignWaiver,
                root, authored, proof, (ulong)parent.Revision + 1), CancellationToken.None);
        var body = await PolicyCampaignContent.WriteAsync(content, host.Tenant, PolicyCampaignContent.WaiverSchema,
            new CampaignWaiverView(root, person, request.Reason, request.ExpiresOn, host.ManagerActor, at), CancellationToken.None);
        var result = await PolicyCampaignContent.WriteResultAsync(content, host.Tenant, PolicyCampaignContent.WaiverSchema,
            body, CancellationToken.None);
        // The forged writer uses a real closure source event as causation, without an admission of this result root.
        var operation = PolicyCampaignContent.Child(Uuid.CreateVersion4(), nameof(ApplyPolicyParticipantResult),
            parent.PendingClosure!.Metadata.EventId, header, owner.Stream, at);
        var forged = new CampaignWaiverApproved(host.Tenant, campaign, root, person, request.Reason, request.ExpiresOn,
            host.ManagerActor, host.ManagerMember, at, operation, intent, null, result);
        DomainEventSeed.Attach(forged, owner.Id, owner.Version + 1);
        await scope.ServiceProvider.GetRequiredService<IEventStore>().AppendAsync(owner.Stream, owner.Version, [forged]);
        Assert.Equal(owner.Version + 1, (ulong)(await host.EventsAsync<DomainEvent>(owner.Stream)).Count);
    }
}
