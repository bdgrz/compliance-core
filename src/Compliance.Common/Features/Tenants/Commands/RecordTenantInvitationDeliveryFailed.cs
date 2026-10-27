using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Tenants;

/// <summary>Records that the delivery attempt definitely failed; a callback of another attempt is stale.</summary>
[Discriminator("bdgrz.tenant-invitation.delivery.record-failed", 1)]
public sealed record RecordTenantInvitationDeliveryFailed(Uuid CommandId, SourceFactProof<TenantMemberInvited> Source,
    Uuid TenantId, Uuid InvitationId, Uuid AttemptId, string FailureCode, DateTimeOffset FailedAt)
    : ITenantInvitationDeliveryReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
