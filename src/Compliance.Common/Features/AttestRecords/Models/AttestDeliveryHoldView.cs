using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.AttestRecords.Commands;
namespace Bdgrz.Compliance.Features.AttestRecords.Models;

/// <summary>Engagement holds still requiring governed delivery or release.</summary>
public sealed record AttestDeliveryHoldView(Uuid TenantId, Uuid ProgramId, Uuid AttestDeliveryHoldId, string Status, string? RecipientReference, DateTimeOffset? DeliveredAt, DateTimeOffset? VerifiedAt);
