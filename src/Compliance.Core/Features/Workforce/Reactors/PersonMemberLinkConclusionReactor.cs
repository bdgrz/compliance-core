using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Workforce.Aggregates;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Bdgrz.Compliance.Features.Workforce.Events;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Reactors;

public sealed partial class PersonMemberLinkConclusionReactor(IProjectionCheckpointStore checkpoints, IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForTenant("people"), WorkloadName),
        IReactorHandler<PersonLinkedToMember>, IReactorHandler<PersonUnlinkedFromMember>
{
    public const string WorkloadName = "PersonMemberLinkConclusionV1";

    public ValueTask<Result> HandleAsync(IReactorContext<PersonLinkedToMember> context, CancellationToken ct) =>
        SendAsync(context, context.Trigger.Header.TenantId, context.Trigger.Header.PersonId, false, ct);

    public ValueTask<Result> HandleAsync(IReactorContext<PersonUnlinkedFromMember> context, CancellationToken ct) =>
        SendAsync(context, context.Trigger.Header.TenantId, context.Trigger.Header.PersonId, true, ct);

    ValueTask<Result> SendAsync<T>(IReactorContext<T> context, Uuid tenant, Uuid person, bool released,
        CancellationToken ct) where T : DomainEvent
    {
        var fact = context.Trigger;
        if (fact.Metadata.IsAudit || !ReferenceEquals(fact, context.Source.Event) ||
            context.Source.Stream != new Person(tenant, person).Stream)
            throw new InvalidOperationException("A member conclusion needs the exact committed Person association fact.");
        return bus.SendReactionAsync(new ConcludeMemberPersonLink(Uuid.CreateVersion5(fact.Metadata.EventId,
            nameof(ConcludeMemberPersonLink)), tenant, NativeFactReference.Of(context.Source.Stream, fact),
            context.Source.ResourceOffset, released), context, ct);
    }
}
