using Bdgrz.Compliance.Features.Examinations.Models;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations.Events;

/// <summary>Retains a received auditor correction while preserving its predecessor in source history.</summary>
[Discriminator("bdgrz.examinations.audit-observation-corrected", 1)]
public sealed record AuditObservationCorrected(Uuid TenantId, Uuid SubjectId, long Revision,
    Uuid? ActorMemberId, Uuid ProgramId, int PreviousSourceVersion,
    AuditObservationReceivedVersion Received, string Reason, DateTimeOffset OccurredAt) : DomainEvent;
