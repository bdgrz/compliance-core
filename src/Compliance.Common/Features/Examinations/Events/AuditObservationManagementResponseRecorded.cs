using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Retains management's response separately from the externally authored wording.</summary>
[Discriminator("bdgrz.examinations.audit-observation-management-response-recorded", 1)]
public sealed record AuditObservationManagementResponseRecorded(Uuid TenantId, Uuid SubjectId,
    long Revision, Uuid? ActorMemberId, Uuid ProgramId,
    AuditObservationManagementResponseView Response, DateTimeOffset OccurredAt) : DomainEvent;
