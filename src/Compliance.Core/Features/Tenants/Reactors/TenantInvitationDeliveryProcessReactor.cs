using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.Tenants.Processes;
using Bdgrz.Compliance.Features.Content;
using Bdgrz.Compliance.Features.Processes;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Reactors;

/// <summary>
///     Starts the delivery process of an invitation from its fact: freezes the one immutable effect payload, makes the
///     durable process record exist before the checkpoint can advance, and drives it. It replaces the reactor that
///     sent mail and wrote the outcome straight back into the invitation stream.
/// </summary>
public sealed partial class TenantInvitationDeliveryProcessReactor(IProjectionCheckpointStore checkpoints,
    ProcessRootReaction process, IImmutableContentStore content)
    : Reactor(checkpoints, EventStreamPattern.ForTenant("tenant-invitations"), "TenantInvitationDeliveryProcessV1"),
      IReactorHandler<TenantMemberInvited>
{
    public async ValueTask HandleAsync(IReactorContext<TenantMemberInvited> context, CancellationToken ct)
    {
        var invited = context.Trigger;
        var payload = await content.WriteJsonAsync(context.Source.Stream.Realm,
            Uuid.CreateVersion5(invited.InvitationId, TenantInvitationDeliveryPayload.Schema),
            TenantInvitationDeliveryPayload.Schema, TenantInvitationDeliveryPayload.Of(invited), ct)
            .ConfigureAwait(false);
        await process.StartAsync(TenantInvitationDeliveryProcess.Name, context, payload, [],
            nameof(TenantInvitationDeliveryProcessReactor), ct).ConfigureAwait(false);
    }
}
