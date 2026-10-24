using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     The period range was amended: the exact persisted amendment intent was applied with the calendar permit that
///     holds the new range. <paramref name="CalendarAuthorizationId" /> and <paramref name="CalendarGeneration" /> name
///     the new reservation; the old one is released only by the settlement that proves this fact.
/// </summary>
[Discriminator("bdgrz.operations.observation-period-amended", 3)]
public sealed record ObservationPeriodAmended(
    Uuid TenantId,
    Uuid SubjectId,
    long Revision,
    Uuid? ActorMemberId,
    TypeIIPeriodView State,
    DateTimeOffset OccurredAt,
    ImmutableContentRef Change,
    OperationProvenance Operation,
    Uuid CalendarAuthorizationId,
    long CalendarGeneration,
    ImmutableContentRef? FirstResponse,
    ImmutableContentRef? OperationResult) : DomainEvent, INativeOperationFact;
