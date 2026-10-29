using Cntryl.Portia;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
namespace Bdgrz.Compliance.Features.AttestRecords.Models;

/// <summary>Attributed delivery and verification receipt.</summary>
public sealed record OwnAttestRecordDeliveryView(Uuid TenantId, Uuid ProgramId, Uuid OwnAttestRecordDeliveryId, string Status, string? RecipientReference, DateTimeOffset? DeliveredAt, DateTimeOffset? VerifiedAt);
