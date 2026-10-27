using Bdgrz.Compliance.Features.Programs.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs.Events;

/// <summary>Records the exact next period and approved plan created from a rollover.</summary>
[Discriminator("bdgrz.programs.program-next-period-created", 1)]
public sealed record ProgramNextPeriodCreated(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    ProgramRolloverView State,
    DateTimeOffset OccurredAt) : DomainEvent;
