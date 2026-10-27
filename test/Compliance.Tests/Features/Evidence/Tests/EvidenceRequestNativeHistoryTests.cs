using System.Collections;
using System.Reflection;
using Bdgrz.Compliance.Tests.Testing;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Applications.Models;
using Bdgrz.Compliance.Features.AuditPackages.Rules;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Incidents.Rules;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Operations.Models;
using Bdgrz.Compliance.Features.Processes.Processes;
using Bdgrz.Compliance.Features.Programs.Readers;
using Bdgrz.Compliance.Features.Providers.Rules;
using Bdgrz.Compliance.Features.Remediation.Rules;
using Bdgrz.Compliance.Features.Risks.Models;
using Bdgrz.Compliance.Features.SourceUse.Models;
using Bdgrz.Compliance.Features.SourceUse.Services;
using Bdgrz.Compliance.Features.TechnologyInventory.Models;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.UserIdentities.Models;
using Bdgrz.Compliance.Tests.Features.AccessControl.Tests;
using Bdgrz.Compliance.Tests.Features.AccessReviews.Tests;
using Bdgrz.Compliance.Tests.Features.Applications.Tests;
using Bdgrz.Compliance.Tests.Features.AuditPackages.Tests;
using Bdgrz.Compliance.Tests.Features.Evaluations.Tests;
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
using Bdgrz.Compliance.Tests.Features.SourceUse.Tests;
using Bdgrz.Compliance.Tests.Features.SystemDescription.Tests;
using Bdgrz.Compliance.Tests.Features.Tenants.Tests;
using Bdgrz.Compliance.Tests.Features.UserIdentities.Tests;
using Bdgrz.Compliance.Tests.Features.Work.Tests;
using Bdgrz.Compliance.Features.Evidence.Aggregates;
using Bdgrz.Compliance.Features.Evidence.Commands;
using Bdgrz.Compliance.Features.Evidence.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Providers.Projections;
using Bdgrz.Compliance.Features.SourceUse.Readers;
using Bdgrz.Compliance.Features.SystemDescription.Aggregates;
namespace Bdgrz.Compliance.Tests.Features.Evidence.Tests;

public sealed class EvidenceRequestNativeHistoryTests
{
    [Fact]
    public async Task ShouldKeepOnlyLifecycleContentAndSelectedFenceGivenLongSettledUseHistory()
    {
        // Arrange
        var tenant = Uuid.CreateVersion4();
        var program = Uuid.CreateVersion4();
        var id = Uuid.CreateVersion4();
        var source = new EvidenceRequest(tenant, program, id);
        var memberId = Uuid.CreateVersion4();
        var actor = ActorReference.ForMember(memberId, "Evidence owner");
        var opened = new EvidenceRequestOpened(tenant, program, id, "MFA policy", "Export the exact policy.",
            memberId, new DateOnly(2026, 10, 15), null, actor, NativeFacts.Now,
            NativeFacts.Root(source, id, nameof(OpenEvidenceRequest), null), NativeFacts.Ref(), NativeFacts.Ref());
        var fulfilled = new EvidenceRequestFulfilled(tenant, program, id, 2, Uuid.CreateVersion4(), actor,
            NativeFacts.Now.AddMinutes(1), NativeFacts.Root(source, Uuid.CreateVersion4(), nameof(FulfilEvidenceRequest), 1),
            NativeFacts.Ref(), NativeFacts.Ref());
        var facts = new List<DomainEvent> { opened, fulfilled };
        ReservationKey? selected = null;
        OperationProvenance? selectedOperation = null;
        ImmutableContentRef? selectedIntent = null;
        for (var index = 0; index < 128; index++)
        {
            var root = Uuid.CreateVersion4();
            var intent = NativeFacts.Ref(SourceUseAuthorizationIntent.Schema);
            var operation = new OperationProvenance(Uuid.CreateVersion4(), root, null, "Evidence use", actor,
                source.Stream, "initial", 2, new string('a', 64), NativeFacts.Now, source.Stream, source.Stream,
                source.Stream);
            facts.Add(new EvidenceRequestActionCompletionUseAuthorized(source.Stream, 2, 1, intent, null,
                operation, null, null));
            facts.Add(new EvidenceRequestActionCompletionUseSettled(source.Stream, 2, 1, intent, null,
                operation with { LogicalId = Uuid.CreateVersion4() }, null, null));
            if (selected is null)
            {
                selected = new ReservationKey(root, source.Stream, 1, source.Stream,
                    SourceUseReservations.PurposeName(SourceUsePurpose.ActionCompletionReview));
                selectedOperation = operation;
                selectedIntent = intent;
            }
        }
        var attached = facts.Select((fact, index) => DomainEventSeed.Attach(fact, source.Id, (ulong)index + 1)).ToArray();
        var store = new InMemoryEventStore();
        await store.AppendAsync(source.Stream, 0, attached, CancellationToken.None);
        var reader = new SourceUseAggregateReader(new ReplayReader(attached), store);

        // Act
        var owner = await reader.HydrateAsync(new EvidenceRequest(tenant, program, id).ForSourceUses(selected!));
        var retained = typeof(EvidenceRequest).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(field => field.GetValue(owner) is ICollection { Count: > 2 }).ToArray();
        var refusal = Assert.Throws<InvalidOperationException>(() => owner.AuthorizeActionCompletionUse(
            selectedOperation! with { LogicalId = Uuid.CreateVersion4() }, selectedIntent!, 1));

        // Assert
        Assert.Empty(retained);
        Assert.Equal(2, owner.Revision);
        Assert.Equal((ulong)attached.Length, owner.Version);
        Assert.Equal(128UL, owner.Uses.Generation);
        Assert.False(owner.Uses.HasActiveUse);
        Assert.True(owner.Uses.Find(selected!)!.IsSettled);
        Assert.Equal(NativeFactReference.Of(source.Stream, attached[2]), owner.Uses.GrantOf(selected!)!.GrantEvent);
        Assert.Equal(opened.Metadata, owner.Opened!.Metadata);
        Assert.Equal(fulfilled.Metadata, owner.FulfilledFact!.Metadata);
        Assert.Equal(fulfilled.Metadata.EventId, owner.Latest!.Metadata.EventId);
        Assert.Equal(fulfilled.ArtifactId, owner.View()!.ArtifactId);
        Assert.Contains("never after its fence", refusal.Message, StringComparison.Ordinal);
        Assert.Empty(new AggregateScenario<EvidenceRequest>(owner).PendingEvents);
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
