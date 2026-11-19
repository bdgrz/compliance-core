using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Workforce.Commands;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Workforce.Reactors;

public sealed partial class PersonMemberLinkAdmissionReactor(IProjectionCheckpointStore checkpoints, IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForTenant("rbac-members"), WorkloadName), IReactorHandler<MemberPersonLinkAdmitted>
{
    public const string WorkloadName = "PersonMemberLinkAdmissionV1";

    public ValueTask<Result> HandleAsync(IReactorContext<MemberPersonLinkAdmitted> context, CancellationToken ct)
    {
        var fact = context.Trigger;
        if (fact.Metadata.IsAudit || !ReferenceEquals(fact, context.Source.Event) ||
            context.Source.Stream != Member.ForVerification(fact.TenantId, fact.MemberId).Stream)
            throw new InvalidOperationException("A person link needs the exact committed Member admission.");
        return bus.SendReactionAsync(new ApplyPersonMemberLink(Uuid.CreateVersion5(fact.Metadata.EventId,
            nameof(ApplyPersonMemberLink)), fact.TenantId, fact.PersonId, NativeFactReference.Of(context.Source.Stream, fact),
            context.Source.ResourceOffset), context, ct);
    }
}
