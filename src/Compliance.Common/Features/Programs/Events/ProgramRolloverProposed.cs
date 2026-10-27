using Bdgrz.Compliance.Features.Programs.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Programs.Events;

/// <summary>Records the ProgramRolloverProposed domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.programs.program-rollover-proposed", 2)]
public sealed record ProgramRolloverProposed(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    ProgramRolloverView State,
    DateTimeOffset OccurredAt) : DomainEvent;
