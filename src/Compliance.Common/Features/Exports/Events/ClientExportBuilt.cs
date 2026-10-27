using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Exports;

/// <summary>Records the ClientExportBuilt domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.exports.client-export-built", 2)]
public sealed record ClientExportBuilt(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    ClientExportView State,
    DateTimeOffset OccurredAt) : DomainEvent;
