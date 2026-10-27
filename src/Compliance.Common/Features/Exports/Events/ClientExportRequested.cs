using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Exports.Commands;
using Bdgrz.Compliance.Features.Exports.Models;
namespace Bdgrz.Compliance.Features.Exports.Events;

/// <summary>Records the ClientExportRequested domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.exports.client-export-requested", 2)]
public sealed record ClientExportRequested(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    ClientExportView State,
    DateTimeOffset OccurredAt) : DomainEvent;
