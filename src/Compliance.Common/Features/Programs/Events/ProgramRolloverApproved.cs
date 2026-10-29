using Cntryl.Portia;
using Bdgrz.Compliance.Features.NamedRecords.Contracts;
using Bdgrz.Compliance.Features.Evidence.Models;
using Bdgrz.Compliance.Features.Programs.Models;
namespace Bdgrz.Compliance.Features.Programs.Events;

/// <summary>Records the ProgramRolloverApproved domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.programs.program-rollover-approved", 2)]
public sealed record ProgramRolloverApproved(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    ProgramRolloverView State,
    DateTimeOffset OccurredAt) : DomainEvent;
