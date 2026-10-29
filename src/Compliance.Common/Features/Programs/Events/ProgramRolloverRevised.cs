using Cntryl.Portia;
using Bdgrz.Compliance.Features.Programs.Models;
namespace Bdgrz.Compliance.Features.Programs.Events;

/// <summary>Records a revised carry-forward selection against the same completed source period.</summary>
[Discriminator("bdgrz.programs.program-rollover-revised", 1)]
public sealed record ProgramRolloverRevised(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    ProgramRolloverView State,
    DateTimeOffset OccurredAt) : DomainEvent;
