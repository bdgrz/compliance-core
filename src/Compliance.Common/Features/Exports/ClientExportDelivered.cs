using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Exports;

/// <summary>Records the ClientExportDelivered domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.exports.client-export-delivered", 2)]
public sealed record ClientExportDelivered(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    ClientExportView State,
    DateTimeOffset OccurredAt) : DomainEvent;
