using Cntryl.Portia;
using Bdgrz.Compliance.Features.FirmServices.Models;
namespace Bdgrz.Compliance.Features.FirmServices.Events;

/// <summary>Retained per-record advisory service fact.</summary>
[Discriminator("bdgrz.firm-services.nonattest.recorded", 1)]
public sealed record NonattestServiceRecorded(Uuid TenantId, Uuid RecordId, long Revision,
    NonattestServiceTerms Terms, FirmServiceReceipt Receipt) : DomainEvent;
