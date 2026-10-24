using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     The period opened: the exact persisted opening intent was applied with the calendar permit that holds its range.
///     <paramref name="CalendarAuthorizationId" /> and <paramref name="CalendarGeneration" /> name that exact
///     reservation, so a later settlement never guesses from the current period state.
/// </summary>
[Discriminator("bdgrz.operations.observation-period-started", 3)]
public sealed record ObservationPeriodStarted(
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
