using Bdgrz.Compliance.Features.Remediation;
using Bdgrz.Compliance.Features.Work;
using Cntryl.Portia;
using Cntryl.Portia.Testing;

namespace Bdgrz.Compliance.Tests.Features.Work;

/// <summary>Attaches canonical stream metadata in tests of the projection store alone.</summary>
static class FindingWorkProjectionTestSource
{
    public static FindingReadSource Unused => new(new NoSource(), new InMemoryEventStore());

    public static ValueTask ApplyAsync(ICorrectiveActionWorkItemProjection projection,
        DomainEvent ev, CancellationToken ct = default) => projection.ApplyAsync(Attach(ev), ct);

    public static ValueTask ApplyAsync(IFindingClosureWorkItemProjection projection,
        DomainEvent ev, CancellationToken ct = default) => projection.ApplyAsync(Attach(ev), ct);

    static DomainEvent Attach(DomainEvent ev)
    {
        var (programId, findingId, revision, at) = ev switch
        {
            FindingRaised fact => (fact.ProgramId, fact.FindingId, fact.Revision, fact.RaisedAt),
            FindingRevised fact => (fact.ProgramId, fact.FindingId, fact.Revision, fact.RevisedAt),
            CorrectiveActionAdded fact => (fact.ProgramId, fact.FindingId, fact.Revision, fact.Action.AddedAt),
            CorrectiveActionCompleted fact => (fact.ProgramId, fact.FindingId, fact.Revision, fact.CompletedAt),
            FindingClosed fact => (fact.ProgramId, fact.FindingId, fact.Revision, fact.Closure.ClosedAt),
            FindingReopened fact => (fact.ProgramId, fact.FindingId, fact.Revision, fact.ReopenedAt),
            FindingAcceptanceLinked fact => (fact.ProgramId, fact.FindingId, fact.Revision, fact.Acceptance.LinkedAt),
            _ => throw new InvalidOperationException("A projection test requires a finding business fact."),
        };
        return DomainEventSeed.Attach(ev, FindingAggregate.StreamId(programId, findingId),
            checked((ulong)revision), occurredOn: at);
    }

    sealed class NoSource : IAggregateReader
    {
        public ValueTask<TAggregate> HydrateAsync<TAggregate>(TAggregate aggregate,
            CancellationToken ct = default) where TAggregate : Aggregate =>
            throw new InvalidOperationException("A projection-only test must not read domain sources.");
    }
}
