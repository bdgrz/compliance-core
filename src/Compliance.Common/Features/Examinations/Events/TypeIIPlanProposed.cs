using Bdgrz.Compliance.Features.Examinations.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations.Events;

/// <summary>Creates a versioned Type II operating plan with exact source and control bindings.</summary>
[Discriminator("bdgrz.examinations.type-i-i-plan-proposed", 2)]
public sealed record TypeIIPlanProposed(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    TypeIIPlanView State,
    DateTimeOffset OccurredAt) : DomainEvent;
