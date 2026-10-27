using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Examinations;

/// <summary>Retains an externally supplied conclusion and exact issued-report provenance.</summary>
[Discriminator("bdgrz.examinations.audit-outcome-recorded", 2)]
public sealed record AuditOutcomeRecorded(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    AuditOutcomeView State,
    DateTimeOffset OccurredAt) : DomainEvent;
