using Bdgrz.Compliance.Features.Examinations.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations.Events;

/// <summary>Records independent executive approval of the reviewed Type II plan version.</summary>
[Discriminator("bdgrz.examinations.type-i-i-plan-approved", 2)]
public sealed record TypeIIPlanApproved(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    TypeIIPlanView State,
    DateTimeOffset OccurredAt) : DomainEvent;
