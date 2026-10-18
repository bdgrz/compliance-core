using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Records the ObservationPeriodCloseProposed domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.auditpopulations.observation-period-close-proposed", 2)]
public sealed record ObservationPeriodCloseProposed(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    TypeIIPeriodView State,
    DateTimeOffset OccurredAt) : DomainEvent;
