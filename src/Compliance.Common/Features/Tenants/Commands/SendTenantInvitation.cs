using Bdgrz.Compliance.Features.Tenants;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Tenants.Events;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants.Commands;

/// <summary>
///     Sends the exact frozen invitation payload to its recipient. The step is the external effect of the delivery
///     process: it records one attempt with a stable identity, sends, and keeps the result on that attempt. A resumed
///     step after an unknown outcome sends the same attempt again, never a new one.
/// </summary>
[Discriminator("bdgrz.tenant-invitation.delivery.send", 1)]
public sealed record SendTenantInvitation(Uuid CommandId, SourceFactProof<TenantMemberInvited> Source, Uuid TenantId,
    Uuid InvitationId, ImmutableContentRef Payload) : ITenantInvitationDeliveryReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
