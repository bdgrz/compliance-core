using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Remediation.Aggregates;
using Bdgrz.Compliance.Features.Remediation.Commands;
using Bdgrz.Compliance.Features.Remediation.Events;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SourceUse.Readers;
using System.Collections;
using System.Reflection;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Remediation;

public sealed class CorrectiveActionNativeHistoryTests
{
    [Fact]
    public async Task ShouldRetainOnlyLifecycleAndPermanentFenceGivenManyClosedParentGenerations()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var findingId = Uuid.CreateVersion4();
        var source = new CorrectiveAction(tenant, program, findingId, id);
        var parent = new FindingAggregate(tenant, program, findingId).Stream;
        var memberId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(memberId, "Action owner");
        var opened = new CorrectiveActionAddedV2(Uuid.CreateVersion4(), tenant, program, findingId, 1,
            NativeFacts.Ref(FindingContent.ActionSchema), NativeFacts.Root(source, Uuid.CreateVersion4(),
                nameof(MaterializeCorrectiveAction), null), NativeFacts.Ref(), null, null);
        var fulfilled = new CorrectiveActionCompletedV2(Uuid.CreateVersion4(), tenant, program, findingId, 2, id,
            "Access removed.", NativeFacts.Ref(FindingContent.EvidenceSchema), memberId, actor, NativeFacts.Now,
            NativeFacts.Root(source, Uuid.CreateVersion4(), nameof(CompleteCorrectiveAction), 1), NativeFacts.Ref(),
            NativeFacts.Ref());
        var facts = new List<DomainEvent> { opened, fulfilled };
        ReservationKey? selected = null;
        OperationProvenance? selectedOperation = null;
        ImmutableContentRef? selectedIntent = null;
        for (var index = 0; index < 128; index++)
        {
            var root = Uuid.CreateVersion4();
            var intent = NativeFacts.Ref(SourceUseAuthorizationIntent.Schema);
            var generation = index + 1L;
            var operation = new OperationProvenance(Uuid.CreateVersion4(), root, null, "Finding closure use", actor,
                source.Stream, "initial", 2, new string('a', 64), NativeFacts.Now, source.Stream, source.Stream,
                parent);
            facts.Add(new CorrectiveActionFindingClosureUseAuthorized(source.Stream, 2, generation, intent, null,
                operation, null, null));
            facts.Add(new CorrectiveActionFindingClosureUseSettled(source.Stream, 2, generation, intent, null,
                operation with { LogicalId = Uuid.CreateVersion4() }, null, null, SourceUseSettlementDisposition.ParentClosed));
            if (selected is null)
            {
                selected = new ReservationKey(root, parent, (ulong)generation, source.Stream,
                    SourceUseReservations.PurposeName(SourceUsePurpose.FindingClosure));
                selectedOperation = operation;
                selectedIntent = intent;
            }
        }
        var attached = facts.Select((fact, index) => DomainEventSeed.Attach(fact, source.Id, (ulong)index + 1)).ToArray();
        var store = new InMemoryEventStore();
        await store.AppendAsync(source.Stream, 0, attached, CancellationToken.None);
        var reader = new SourceUseAggregateReader(new ReplayReader(attached), store);

        // Act
        var owner = await reader.HydrateAsync(new CorrectiveAction(tenant, program, findingId, id).ForSourceUses(selected!));
        var retained = typeof(CorrectiveAction).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => field.GetValue(owner) is ICollection { Count: > 2 }).ToArray();
        var refusal = Assert.Throws<InvalidOperationException>(() => owner.AuthorizeFindingClosureUse(
            selectedOperation! with { LogicalId = Uuid.CreateVersion4() }, selectedIntent!, 1));

        // Assert
        Assert.Empty(retained);
        Assert.Equal(2, owner.Revision);
        Assert.Equal((ulong)attached.Length, owner.Version);
        Assert.Equal(128UL, owner.Uses.Generation);
        Assert.False(owner.Uses.HasActiveUse);
        Assert.True(owner.Uses.Find(selected!)!.IsSettled);
        Assert.Equal(NativeFactReference.Of(source.Stream, attached[2]), owner.Uses.GrantOf(selected!)!.GrantEvent);
        Assert.Equal(opened.Metadata, owner.Added!.Metadata);
        Assert.Equal(fulfilled.Metadata, owner.Completed!.Metadata);
        Assert.Equal(fulfilled.Metadata.EventId, owner.Latest!.Metadata.EventId);
        Assert.Contains("fenced by a closed finding generation", owner.PrepareComplete(2).Error!.Message, StringComparison.Ordinal);
        Assert.Contains("never after its fence", refusal.Message, StringComparison.Ordinal);
        Assert.Empty(new AggregateScenario<CorrectiveAction>(owner).PendingEvents);
    }

    sealed class ReplayReader(DomainEvent[] facts) : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate, CancellationToken ct = default)
            where TAggregate : Aggregate
        {
            ct.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new AggregateScenario<TAggregate>(aggregate)
                .Given(facts[(int)aggregate.Version..]).Aggregate);
        }
    }
}
