using Cntryl.Portia;
using Bdgrz.Compliance.Features.Examinations.Models;
namespace Bdgrz.Compliance.Features.Examinations.Events;

/// <summary>Retains received auditor wording and its exact source provenance.</summary>
[Discriminator("bdgrz.examinations.audit-observation-recorded", 2)]
public sealed record AuditObservationRecorded(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    Uuid ProgramId,
    AuditObservationReceivedVersion Received,
    DateTimeOffset OccurredAt) : DomainEvent;
