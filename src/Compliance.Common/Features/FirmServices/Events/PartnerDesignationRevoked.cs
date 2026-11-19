using Cntryl.Portia;
using Bdgrz.Compliance.Features.FirmServices.Models;
namespace Bdgrz.Compliance.Features.FirmServices.Events;

/// <summary>One immutable grant or revocation of a partner duty.</summary>
[Discriminator("bdgrz.firm-services.partner.revoked", 1)]
public sealed record PartnerDesignationRevoked(Uuid UserId, string Duty, long Revision, string Reason,
    FirmServiceReceipt Receipt) : DomainEvent;
