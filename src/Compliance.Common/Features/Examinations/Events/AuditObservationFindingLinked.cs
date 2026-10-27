using Bdgrz.Compliance.Features.Examinations.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations.Events;

/// <summary>Links an exact finding source revision without taking ownership of its remediation.</summary>
[Discriminator("bdgrz.examinations.audit-observation-finding-linked", 2)]
public sealed record AuditObservationFindingLinked(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    Uuid ProgramId,
    AuditObservationFindingLinkView Link,
    DateTimeOffset OccurredAt) : DomainEvent;
