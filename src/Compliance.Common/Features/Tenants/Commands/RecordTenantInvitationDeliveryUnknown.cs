using Cntryl.Portia;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.Tenants.Contracts;
using Bdgrz.Compliance.Features.Tenants.Events;

namespace Bdgrz.Compliance.Features.Tenants.Commands;

/// <summary>Records an uncertain provider outcome; the same invitation attempt must never be sent again.</summary>
[Discriminator("bdgrz.tenant-invitation.delivery.record-unknown", 1)]
public sealed record RecordTenantInvitationDeliveryUnknown(Uuid CommandId,
    SourceFactProof<TenantMemberInvited> Source, Uuid TenantId, Uuid InvitationId, Uuid AttemptId,
    string FailureCode, DateTimeOffset RecordedAt) : ITenantInvitationDeliveryReceiver
{
    Uuid IIdentifiedRequest.RequestId => CommandId;
}
