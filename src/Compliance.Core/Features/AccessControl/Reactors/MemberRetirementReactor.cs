using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Services;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.AccessControl.Aggregates;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Events;
namespace Bdgrz.Compliance.Features.AccessControl.Reactors;

public sealed partial class MemberRetirementReactor(IProjectionCheckpointStore checkpoints, IRequestBus bus)
    : Reactor(checkpoints, EventStreamPattern.ForTenant("rbac-members"), "MemberRetirementV1"),
      IReactorHandler<MemberDeprovisioned>
{
    public async ValueTask HandleAsync(IReactorContext<MemberDeprovisioned> context, CancellationToken ct)
    {
        var source = context.Trigger;
        if (context.Source.Stream != new Member(source.TenantId, source.UserId).Stream ||
            source.MemberId != RbacIds.Member(source.TenantId, source.UserId) || source.Metadata.IsAudit ||
            context.Source.ResourceOffset != source.Metadata.AggregateVersion - 1)
            throw new InvalidOperationException("Retirement recovery requires its exact authoritative member source.");
        var request = new CompleteMemberRetirement(source.TenantId, source.MemberId, source.MembershipEpisodeId,
            source.Metadata.EventId, MemberRetirementEffects.CompletionId(source.Metadata.EventId,
                source.MemberId, source.MembershipEpisodeId));
        await bus.SendReactionAsync(request, context, ct).ConfigureAwait(false);
    }
}
