using Bdgrz.Compliance.Features.Operations;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Transient verified period rule mutation; never emitted or retained by the command owner.</summary>
public sealed record ObservationPeriodCloseFrozenBody(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    TypeIIPeriodView State,
    DateTimeOffset OccurredAt,
    Uuid CalendarAuthorizationId,
    long CalendarGeneration);
