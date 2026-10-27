using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using System.Text.Json;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;
using Cntryl.Portia.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bdgrz.Compliance.Tests.Testing;

/// <summary>Builds the provenance and content references that native facts carry, for owner-level tests.</summary>
static class NativeFacts
{
    public static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A content reference of the given schema; the owner never reads it.</summary>
    public static ImmutableContentRef Ref(string schema = "test.v1") =>
        new(Uuid.CreateVersion4(), new string('a', 64), 2, "application/json", schema);

    /// <summary>The provenance of a root operation on the owner's own stream.</summary>
    public static OperationProvenance Root(Aggregate owner, Uuid logicalId, string commandKind, long? expectedRevision,
        Uuid? actorMemberId = null, DateTimeOffset? at = null) => RootOperation.Provenance(logicalId, commandKind,
        owner.Stream, ActorReference.ForMember(actorMemberId ?? Uuid.CreateVersion4(), "Tester"), expectedRevision,
        new string('b', 64), at ?? Now);

    /// <summary>The facts the owner raised since it was opened, after a JSON round trip and with broker metadata.</summary>
    public static DomainEvent[] Raised<TAggregate>(TAggregate owner, params DateTimeOffset[] occurredOn)
        where TAggregate : Aggregate =>
        new AggregateScenario<TAggregate>(owner).PendingEvents.Select((fact, index) => occurredOn.Length > index
            ? DomainEventSeed.Attach(RoundTrip(fact), owner.Id, (ulong)index + 1, occurredOn: occurredOn[index])
            : DomainEventSeed.Attach(RoundTrip(fact), owner.Id, (ulong)index + 1)).ToArray();

    /// <summary>A fresh owner replayed from the facts.</summary>
    public static TAggregate Replay<TAggregate>(TAggregate fresh, IEnumerable<DomainEvent> facts)
        where TAggregate : Aggregate => new AggregateScenario<TAggregate>(fresh).Given(facts.ToArray()).Aggregate;

    /// <summary>How many facts (audit records excluded) the tenant's area holds.</summary>
    public static async Task<long> CountAsync(IServiceProvider provider, Uuid tenantId, string area)
    {
        var count = 0L;
        await foreach (var record in provider.GetRequiredService<IDomainEventReader>().ReadAsync(
                           EventStreamPattern.ForPattern(tenantId.ToString(), area), EventCursor.Start,
                           CancellationToken.None))
            if (!record.Event.Metadata.IsAudit)
                count++;
        return count;
    }

    static DomainEvent RoundTrip(DomainEvent fact) => (DomainEvent)JsonSerializer.Deserialize(
        JsonSerializer.Serialize(fact, fact.GetType(), ComplianceCoreJsonContext.Default), fact.GetType(),
        ComplianceCoreJsonContext.Default)!;
}
