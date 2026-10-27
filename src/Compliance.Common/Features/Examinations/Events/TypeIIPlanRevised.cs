using Cntryl.Portia;
using Bdgrz.Compliance.Features.AccessControl.Commands;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Examinations.Models;
namespace Bdgrz.Compliance.Features.Examinations.Events;

/// <summary>Appends a revised immutable Type II plan after a review requests changes.</summary>
[Discriminator("bdgrz.examinations.type-i-i-plan-revised", 1)]
public sealed record TypeIIPlanRevised(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    TypeIIPlanView State,
    DateTimeOffset OccurredAt) : DomainEvent;
