using Cntryl.Portia;
using Bdgrz.Compliance.Features.FirmServices.Models;
namespace Bdgrz.Compliance.Features.FirmServices.Events;

/// <summary>Retained per-record advisory service fact.</summary>
[Discriminator("bdgrz.firm-services.nonattest.voided", 1)]
public sealed record NonattestServiceVoided(Uuid TenantId, Uuid RecordId, long Revision,
    string Reason, FirmServiceReceipt Receipt) : DomainEvent;
