using Bdgrz.Compliance.Features.Exports.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Exports.Events;

/// <summary>Records the audit-software delivery decision for an exact attest package.</summary>
[Discriminator("bdgrz.exports.client-export-attest-package-delivery-recorded", 1)]
public sealed record ClientExportAttestPackageDeliveryRecorded(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    ClientExportView State,
    DateTimeOffset OccurredAt) : DomainEvent;
