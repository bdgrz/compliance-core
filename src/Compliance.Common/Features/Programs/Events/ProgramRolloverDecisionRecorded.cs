using Cntryl.Portia;
using Bdgrz.Compliance.Features.Programs.Models;
using Bdgrz.Compliance.Features.Retention.Models;
namespace Bdgrz.Compliance.Features.Programs.Events;

/// <summary>Records an explicit rejection of a carry-forward proposal version.</summary>
[Discriminator("bdgrz.programs.program-rollover-decision-recorded", 1)]
public sealed record ProgramRolloverDecisionRecorded(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    ProgramRolloverView State,
    DateTimeOffset OccurredAt) : DomainEvent;
