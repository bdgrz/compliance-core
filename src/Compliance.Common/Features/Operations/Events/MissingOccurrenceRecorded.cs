using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
namespace Bdgrz.Compliance.Features.Operations.Events;

/// <summary>Records the MissingOccurrenceRecorded domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.operations.missing-occurrence-recorded", 1)]
public sealed record MissingOccurrenceRecorded(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
