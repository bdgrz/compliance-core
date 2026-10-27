using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
namespace Bdgrz.Compliance.Features.Programs.Events;

/// <summary>Records the NextObservationPeriodStarted domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.programs.next-observation-period-started", 1)]
public sealed record NextObservationPeriodStarted(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    DateTimeOffset OccurredAt) : DomainEvent;
