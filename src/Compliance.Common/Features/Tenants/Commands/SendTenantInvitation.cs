using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.PolicyDistribution.Queries;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Events;
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
