using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

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
