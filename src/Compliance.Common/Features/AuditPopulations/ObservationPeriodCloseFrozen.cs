using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditPopulations;

/// <summary>
///     The period close was frozen. <paramref name="CalendarAuthorizationId" /> and
///     <paramref name="CalendarGeneration" /> name the exact live calendar reservation the close releases, so a
///     release never depends on re-reading the period.
/// </summary>
[Discriminator("bdgrz.auditpopulations.observation-period-close-frozen", 3)]
public sealed record ObservationPeriodCloseFrozen(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    TypeIIPeriodView State,
    DateTimeOffset OccurredAt,
    Uuid CalendarAuthorizationId,
    long CalendarGeneration) : DomainEvent;
