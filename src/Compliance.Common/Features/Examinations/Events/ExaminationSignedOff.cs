using Bdgrz.Compliance.Features.Examinations.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations.Events;

/// <summary>Records management's immutable review of the exact examination inputs and outcomes.</summary>
[Discriminator("bdgrz.examinations.examination-signed-off", 2)]
public sealed record ExaminationSignedOff(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    ExaminationSignOffView State,
    DateTimeOffset OccurredAt) : DomainEvent;
