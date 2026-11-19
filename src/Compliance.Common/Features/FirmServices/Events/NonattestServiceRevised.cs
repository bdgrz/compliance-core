using Cntryl.Portia;
using Bdgrz.Compliance.Features.FirmServices.Models;
namespace Bdgrz.Compliance.Features.FirmServices.Events;

/// <summary>Retained per-record advisory service fact.</summary>
[Discriminator("bdgrz.firm-services.nonattest.revised", 1)]
public sealed record NonattestServiceRevised(Uuid TenantId, Uuid RecordId, long Revision,
    NonattestServiceTerms Terms, string Reason, FirmServiceReceipt Receipt) : DomainEvent;
