using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.PolicyDistribution.Aggregates;
using Bdgrz.Compliance.Features.PolicyDistribution.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Events;
using Bdgrz.Compliance.Features.PolicyDistribution.Readers;
using System.Runtime.CompilerServices;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.PolicyDistribution;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Features.PolicyDistribution;

public sealed class PolicyCampaignNativeHistoryTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ShouldRejectUnselectedRosterOrResultRootReuseGivenDefaultOrExecutorRead(bool participant, bool execute)
    {
        // Arrange
        await using var host = new CampaignHost();
        var campaignId = await host.LaunchAsync(CampaignHost.PolicySubject, host.OfflinePerson);
        DomainEvent malformed;
        EventStreamAddress stream;
        Uuid id;
        ulong version;
        if (participant)
        {
            Assert.True((await CampaignSeed.ApproveWaiverAsync(host.Provider, host.Tenant, host.Program, campaignId,
                host.OfflinePerson, "Original admitted exception.", host.Today.AddDays(1), host.ManagerActor,
                host.Clock.GetUtcNow())).IsSuccess);
            var owner = await host.ParticipantAsync(campaignId, host.OfflinePerson);
            var original = Assert.Single(owner.Waivers);
            malformed = original with { Operation = original.Operation with { LogicalId = Uuid.CreateVersion4() } };
            (stream, id, version) = (owner.Stream, owner.Id, owner.Version);
        }
        else
        {
            var empty = new RosterAudience(new Dictionary<Uuid, RosterMatch>(), new HashSet<Uuid>());
            var snapshot = Uuid.CreateVersion4();
            Assert.True((await CampaignSeed.ReconcileAsync(host.Provider, host.Tenant, host.Program, campaignId,
                snapshot, new string('a', 64), empty, new HashSet<Uuid> { host.OfflinePerson }, host.ManagerActor,
                host.Clock.GetUtcNow())).IsSuccess);
            var owner = await host.CampaignAsync(campaignId);
            var original = Assert.Single(owner.Amendments);
            var root = Uuid.CreateVersion4();
            var request = new ReconcileCampaignAudience(root, host.Tenant, host.Program, campaignId, snapshot, owner.Revision);
            malformed = original with
            {
                Reconciliation = original.Reconciliation + 1,
                Operation = original.Operation with
                {
                    LogicalId = root,
                    RootOperationId = root,
                    OriginalExpectedRevision = (ulong)owner.Revision,
                    AuthoredIntentSha256 = AuthoredIntentDigest.Compute(request)
                }
            };
            (stream, id, version) = (owner.Stream, owner.Id, owner.Version);
        }
        DomainEventSeed.Attach(malformed, id, version + 1);
        await host.Scope.ServiceProvider.GetRequiredService<IEventStore>().AppendAsync(stream, version, [malformed]);
        var entered = false;

        // Act
        var failure = await Record.ExceptionAsync(async () =>
        {
            if (participant)
            {
                var owner = new PolicyCampaignParticipant(host.Tenant, campaignId, host.OfflinePerson);
                if (execute)
                    await host.Scope.ServiceProvider.GetRequiredService<IAggregateExecutor>().ExecuteAsync(owner, _ =>
                    { entered = true; return AggregateOutcome.Discard(Result.Success); }, new RequestDispatchContext(RequestActor.System));
                else
                    await host.Scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(owner);
            }
            else
            {
                var owner = new PolicyDistributionCampaign(host.Tenant, campaignId);
                if (execute)
                    await host.Scope.ServiceProvider.GetRequiredService<IAggregateExecutor>().ExecuteAsync(owner, _ =>
                    { entered = true; return AggregateOutcome.Discard(Result.Success); }, new RequestDispatchContext(RequestActor.System));
                else
                    await host.Scope.ServiceProvider.GetRequiredService<IAggregateReader>().HydrateAsync(owner);
            }
        });

        // Assert
        Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.False(entered);
        Assert.Equal(version + 1, (ulong)(await host.EventsAsync<DomainEvent>(stream)).Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRejectActualPrefixSubstitutionOrTruncationGivenHydratedParticipantCut(bool truncate)
    {
        // Arrange
        await using var host = new CampaignHost();
        var campaignId = await host.LaunchAsync(CampaignHost.PolicySubject, host.OfflinePerson);
        for (var index = 0; index < 2; index++)
            Assert.True((await CampaignSeed.ApproveWaiverAsync(host.Provider, host.Tenant, host.Program, campaignId,
                host.OfflinePerson, "Exception " + index, host.Today.AddDays(1), host.ManagerActor,
                host.Clock.GetUtcNow())).IsSuccess);
        var owner = await host.ParticipantAsync(campaignId, host.OfflinePerson);
        var facts = owner.Facts.ToArray();
        if (truncate)
            facts = facts[..^1];
        else
        {
            var original = (CampaignWaiverApproved)facts[0];
            var changed = original with { Reason = "Another complete historical reason." };
            changed.AttachMetadata(original.Metadata);
            facts[0] = changed;
        }
        var reader = new PolicyCampaignAggregateReader(host.Scope.ServiceProvider.GetRequiredService<IAggregateReader>(),
            new Records(owner.Stream, facts));

        // Act
        var failure = await Record.ExceptionAsync(async () => await reader.HydrateAsync(new PolicyCampaignParticipant(
            host.Tenant, campaignId, host.OfflinePerson)));

        // Assert
        if (truncate)
            Assert.IsType<ImmutableContentUnavailableException>(failure);
        else
            Assert.IsType<ImmutableContentIntegrityException>(failure);
        Assert.Equal(2, owner.Revision);
        Assert.Equal(2, (await host.EventsAsync<DomainEvent>(owner.Stream)).Count);
    }

    [Fact]
    public async Task ShouldSelectOriginalTerminalClosureGivenLaterGenerationsAndDifferentReceiverCommand()
    {
        // Arrange
        await using var host = new CampaignHost();
        await host.SeedAsync();
        var campaignId = await host.LaunchAsync();
        var first = new CloseCampaign(Uuid.CreateVersion4(), host.Tenant, host.Program, campaignId, "First generation.", 1);
        var admitted = await host.SendAsync(first);
        Assert.True(admitted.IsSuccess, admitted.Error?.Message);
        var snapshot = await host.CampaignAsync(campaignId);
        var accepted = snapshot.ClosureFor(first.CommandId)!;
        Assert.True((await host.DispatchAsync(new AbortPolicyCampaignClosure(Uuid.CreateVersion4(), first.CommandId,
            snapshot.Stream, accepted.Generation, accepted.Revision, accepted.Intent))).IsSuccess);
        var next = await host.SendAsync(new CloseCampaign(Uuid.CreateVersion4(), host.Tenant, host.Program,
            campaignId, "Next generation.", 3));
        Assert.True(next.IsSuccess, next.Error?.Message);
        var before = await host.CampaignAsync(campaignId);

        // Act
        var retry = await host.DispatchAsync(new AbortPolicyCampaignClosure(Uuid.CreateVersion4(), first.CommandId,
            snapshot.Stream, accepted.Generation, accepted.Revision, accepted.Intent));
        var after = await host.CampaignAsync(campaignId);

        // Assert
        Assert.True(retry.IsSuccess, retry.Error?.Message);
        var known = Assert.IsType<Bdgrz.Compliance.Features.Processes.Models.ReceiverOutcome.Superseded>(retry.Value);
        Assert.Equal(before.CanceledFor(first.CommandId)!.Metadata.EventId, known.Evidence!.EventId);
        Assert.Equal(before.PendingClosure, after.PendingClosure);
        Assert.Equal(2, after.Generation);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
    }

    [Fact]
    public async Task ShouldRejectMalformedUnselectedHistoricalRootGivenAcceptedReceiverRetry()
    {
        // Arrange
        await using var host = new CampaignHost();
        var campaignId = await host.LaunchAsync(CampaignHost.PolicySubject, host.OfflinePerson);
        var root = Uuid.CreateVersion4();
        for (var index = 0; index < 2; index++)
            Assert.True((await CampaignSeed.ApproveWaiverAsync(host.Provider, host.Tenant, host.Program, campaignId,
                host.OfflinePerson, "Exception " + index, host.Today.AddDays(1), host.ManagerActor,
                host.Clock.GetUtcNow(), index == 0 ? root : null)).IsSuccess);
        var parent = await host.CampaignAsync(campaignId);
        var admitted = parent.AdmittedFor(root)!;
        var participant = await host.ParticipantAsync(campaignId, host.OfflinePerson);
        var original = participant.Waivers[0];
        var malformed = original with { Operation = original.Operation with { LogicalId = Uuid.CreateVersion4() } };
        DomainEventSeed.Attach(malformed, participant.Id, participant.Version + 1);
        await host.Scope.ServiceProvider.GetRequiredService<IEventStore>().AppendAsync(participant.Stream,
            participant.Version, [malformed]);
        var command = new ApplyPolicyParticipantResult(Uuid.CreateVersion5(root, "apply"),
            NativeFactReference.Of(parent.Stream, admitted), participant.Stream, root, admitted.Generation, null,
            admitted.Intent, campaignId, host.OfflinePerson, CampaignParticipantResultKind.Waiver);

        // Act
        var replay = await host.DispatchAsync(command);

        // Assert
        Assert.False(replay.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, replay.Error?.Kind);
        Assert.False(replay.Error!.IsTransient);
        Assert.Equal(3, (await host.EventsAsync<CampaignWaiverApproved>(participant.Stream)).Count);
        Assert.Equal(original.Operation.LogicalId, command.CommandId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShouldRequireExactSelectedRosterAbsenceGivenExistingCampaignAppend(bool alreadySeen)
    {
        // Arrange
        await using var host = new CampaignHost();
        var campaignId = await host.LaunchAsync(CampaignHost.PolicySubject, host.OfflinePerson);
        var snapshot = Uuid.CreateVersion4();
        var empty = new RosterAudience(new Dictionary<Uuid, RosterMatch>(), new HashSet<Uuid>());
        Assert.True((await CampaignSeed.ReconcileAsync(host.Provider, host.Tenant, host.Program, campaignId, snapshot,
            new string('a', 64), empty, new HashSet<Uuid> { host.OfflinePerson }, host.ManagerActor,
            host.Clock.GetUtcNow())).IsSuccess);
        var before = await host.CampaignAsync(campaignId);
        var prior = Assert.Single(before.Amendments);
        var root = Uuid.CreateVersion4();
        var target = alreadySeen ? snapshot : Uuid.CreateVersion4();
        var request = new ReconcileCampaignAudience(root, host.Tenant, host.Program, campaignId, target, before.Revision);
        var provenance = PolicyCampaignContent.Provenance(root, nameof(ReconcileCampaignAudience), before.Stream,
            before.Stream, host.ManagerActor, before.Revision, AuthoredIntentDigest.Compute(request), host.Clock.GetUtcNow());

        // Act
        var attempted = await host.Scope.ServiceProvider.GetRequiredService<IAggregateExecutor>().ExecuteAsync(
            new PolicyDistributionCampaign(host.Tenant, campaignId, root, alreadySeen ? snapshot : Uuid.Empty),
            owner => AggregateOutcome.Commit(owner.Reconcile(provenance, target, new string('b', 64), host.Today,
                prior.AmendmentsManifest, host.ManagerActor, host.Clock.GetUtcNow(), prior.FirstResponse!, prior.OperationResult!)),
            new RequestDispatchContext(RequestActor.System));
        var after = await host.CampaignAsync(campaignId);

        // Assert
        Assert.False(attempted.IsSuccess);
        Assert.Equal(RequestErrorKind.Conflict, attempted.Error?.Kind);
        Assert.Equal(!alreadySeen, attempted.Error!.IsTransient);
        Assert.Equal(before.CommittedStreamPosition, after.CommittedStreamPosition);
        Assert.Single(after.Amendments);
    }

    sealed class Records(EventStreamAddress stream, DomainEvent[] facts) : IDomainEventReader
    {
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamAddress address, ulong fromOffset,
            CancellationToken ct = default) => Enumerate(address, fromOffset, ct);
        public IAsyncEnumerable<DomainEventRecord> ReadAsync(EventStreamPattern pattern, EventCursor cursor,
            CancellationToken ct = default) => Enumerate(stream, 0, ct);
        async IAsyncEnumerable<DomainEventRecord> Enumerate(EventStreamAddress address, ulong fromOffset,
            [EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            if (address != stream)
                yield break;
            for (var index = fromOffset; index < (ulong)facts.Length; index++)
            {
                ct.ThrowIfCancellationRequested();
                yield return new DomainEventRecord(stream, facts[index], index, EventCursor.Start);
            }
        }
    }
}
