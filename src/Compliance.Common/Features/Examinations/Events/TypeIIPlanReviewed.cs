using Bdgrz.Compliance.Features.Examinations.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations.Events;

/// <summary>Records the independent review of the current Type II plan version.</summary>
[Discriminator("bdgrz.examinations.type-i-i-plan-reviewed", 1)]
public sealed record TypeIIPlanReviewed(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    TypeIIPlanView State,
    DateTimeOffset OccurredAt) : DomainEvent;
