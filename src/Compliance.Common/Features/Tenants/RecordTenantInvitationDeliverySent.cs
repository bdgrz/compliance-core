using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>Records that the provider accepted the delivery attempt; a callback of another attempt is stale.</summary>
[Discriminator("bdgrz.tenant-invitation.delivery.record-sent", 1)]
public sealed record RecordTenantInvitationDeliverySent(Uuid CommandId, SourceFactProof<TenantMemberInvited> Source,
    Uuid TenantId, Uuid InvitationId, Uuid AttemptId, DateTimeOffset SentAt) : ITenantInvitationDeliveryReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
