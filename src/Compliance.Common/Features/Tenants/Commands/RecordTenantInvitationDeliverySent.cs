using Cntryl.Portia;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Events;
namespace Bdgrz.Compliance.Features.Tenants.Commands;

/// <summary>Records that the provider accepted the delivery attempt; a callback of another attempt is stale.</summary>
[Discriminator("bdgrz.tenant-invitation.delivery.record-sent", 1)]
public sealed record RecordTenantInvitationDeliverySent(Uuid CommandId, SourceFactProof<TenantMemberInvited> Source,
    Uuid TenantId, Uuid InvitationId, Uuid AttemptId, DateTimeOffset SentAt) : ITenantInvitationDeliveryReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
