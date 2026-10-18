using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>Records the TypeIIPeriodCloseApproved domain fact for one tenant-owned subject revision.</summary>
[Discriminator("bdgrz.auditpopulations.type-i-i-period-close-approved", 2)]
public sealed record TypeIIPeriodCloseApproved(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    TypeIIPeriodView State,
    DateTimeOffset OccurredAt) : DomainEvent;
