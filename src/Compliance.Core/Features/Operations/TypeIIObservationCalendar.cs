using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>
///     The one stable program guard of Type II observation ranges. It owns the reservations of every period of the
///     program, keyed by the root operation that acquired them, and the inclusive non-overlap rule between periods:
///     a range may not overlap a live (pending or active) range of any other period, while an amendment's own period
///     keeps its old range held next to the new one until the amendment is applied. A reservation settled before it
///     was ever granted leaves a fence, so a delayed acquisition is refused. Reservations are rebuilt from the
///     ordered stream alone; time never releases anything.
/// </summary>
public sealed class TypeIIObservationCalendar : Aggregate
{
    /// <summary>Stream area of every program calendar.</summary>
    public const string Area = "type-ii-observation-calendars";

    readonly Dictionary<Uuid, TypeIIRangeReservation> _reservations = [];
    long _revision;
    long _generation;

    /// <summary>Opens the calendar of one program, selecting the logical operation to resolve.</summary>
    public TypeIIObservationCalendar(Uuid tenantId, Uuid programId, Uuid selectedLogicalId = default)
        : base(programId, new EventStreamAddress(tenantId.ToString(), Area, programId.ToString()))
    {
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<TypeIIPeriodRangeAuthorized>(Apply);
        On<TypeIIPeriodRangeSettled>(Apply);
    }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The number of facts on the calendar.</summary>
    public long Revision => _revision;

    /// <summary>The calendar's own monotonic reservation counter: the number of authorizations so far.</summary>
    public long ReservationGeneration => _generation;

    /// <summary>Every reservation and fence, by root operation.</summary>
    public IReadOnlyCollection<TypeIIRangeReservation> Reservations => _reservations.Values;

    /// <summary>The reservation or fence of a root operation, once recorded.</summary>
    public TypeIIRangeReservation? Find(Uuid operationId) => _reservations.GetValueOrDefault(operationId);

    /// <summary>The reservation granted by the authorization fact with this event id, once recorded.</summary>
    public TypeIIRangeReservation? FindByAuthorization(Uuid eventId) => _reservations.Values.FirstOrDefault(
        reservation => reservation.AuthorizedFact?.Metadata?.EventId == eventId);

    /// <summary>
    ///     The first live reservation of another period whose range overlaps <paramref name="range" />, using the
    ///     inclusive date rule. The period's own reservations never conflict with it.
    /// </summary>
    public TypeIIRangeReservation? FindOverlap(Uuid periodId, ObservationRange range)
    {
        ArgumentNullException.ThrowIfNull(range);
        return _reservations.Values.Where(reservation => reservation.IsLive && reservation.PeriodId != periodId &&
                reservation.Range!.Overlaps(range))
            .OrderBy(reservation => reservation.Generation).FirstOrDefault();
    }

    /// <summary>Holds the new range of an opening or amendment; the replaced reservation stays held.</summary>
    public Result Authorize(OperationProvenance operation, ImmutableContentRef intent, Uuid periodId,
        TypeIIRangeOperationKind kind, ObservationRange newRange, Uuid? priorOperationId)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(newRange);
        if (operation.Owner != Stream || periodId == Uuid.Empty || !newRange.IsValid ||
            kind == TypeIIRangeOperationKind.Opening != (priorOperationId is null))
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A range authorization names its calendar, period, a valid range and, for an amendment, its prior range."));
        var refused = Check(operation.RootOperationId, periodId, newRange, priorOperationId);
        if (refused is not null)
            return Result.Failure(refused);
        RaiseEvent(new TypeIIPeriodRangeAuthorized(Stream, _revision + 1, TypeIIRangeEffects.Generation, intent, null,
            operation, null, null, periodId, kind, newRange, priorOperationId));
        return Result.Success;
    }

    /// <summary>
    ///     Settles exactly one reservation: activates a pending one (releasing the one it replaced), releases a live
    ///     one, or fences an operation that was never granted. It never touches any other reservation.
    /// </summary>
    public Result Settle(OperationProvenance operation, ImmutableContentRef intent, Uuid periodId,
        TypeIIRangeSettlementKind settlement, Uuid targetOperationId, Uuid? releasedOperationId)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream || periodId == Uuid.Empty || targetOperationId == Uuid.Empty ||
            releasedOperationId is not null && settlement != TypeIIRangeSettlementKind.Activated)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A settlement names its calendar, period and the exact reservation it settles."));
        var refused = CheckSettlement(periodId, settlement, targetOperationId, releasedOperationId);
        if (refused is not null)
            return Result.Failure(refused);
        RaiseEvent(new TypeIIPeriodRangeSettled(Stream, _revision + 1, TypeIIRangeEffects.Generation, intent, null,
            operation, null, null, periodId, settlement, targetOperationId, releasedOperationId));
        return Result.Success;
    }

    void Apply(TypeIIPeriodRangeAuthorized ev)
    {
        Acceptance.Observe(ev);
        var failure = ev.Owner != Stream || ev.Operation.Owner != Stream || ev.Revision != _revision + 1 ||
            ev.Generation != TypeIIRangeEffects.Generation || !ev.NewRange.IsValid || ev.PeriodId == Uuid.Empty
            || ev.Kind == TypeIIRangeOperationKind.Opening != (ev.PriorOperationId is null)
            ? new RequestError(RequestErrorKind.Validation, "invalid")
            : Check(ev.Operation.RootOperationId, ev.PeriodId, ev.NewRange, ev.PriorOperationId);
        if (failure is not null)
            throw new InvalidOperationException($"The range authorization is invalid: {failure.Message}");
        _revision = ev.Revision;
        _generation++;
        _reservations.Add(ev.Operation.RootOperationId, new TypeIIRangeReservation(ev.Operation.RootOperationId,
            ev.PeriodId, TypeIIRangeReservationState.Pending, ev.Kind, ev.NewRange, _generation, ev.PriorOperationId,
            ev, null));
    }

    void Apply(TypeIIPeriodRangeSettled ev)
    {
        Acceptance.Observe(ev);
        var failure = ev.Owner != Stream || ev.Operation.Owner != Stream || ev.Revision != _revision + 1 ||
            ev.Generation != TypeIIRangeEffects.Generation ||
            ev.ReleasedOperationId is not null && ev.Settlement != TypeIIRangeSettlementKind.Activated
            ? new RequestError(RequestErrorKind.Validation, "invalid")
            : CheckSettlement(ev.PeriodId, ev.Settlement, ev.TargetOperationId, ev.ReleasedOperationId);
        if (failure is not null)
            throw new InvalidOperationException($"The range settlement is invalid: {failure.Message}");
        _revision = ev.Revision;
        switch (ev.Settlement)
        {
            case TypeIIRangeSettlementKind.Fenced:
                _reservations.Add(ev.TargetOperationId, new TypeIIRangeReservation(ev.TargetOperationId, ev.PeriodId,
                    TypeIIRangeReservationState.Fenced, TypeIIRangeOperationKind.Opening, null, 0, null, null, ev));
                break;
            case TypeIIRangeSettlementKind.Released:
                _reservations[ev.TargetOperationId] = _reservations[ev.TargetOperationId] with
                {
                    State = TypeIIRangeReservationState.Released,
                    SettledFact = ev,
                };
                break;
            default:
                _reservations[ev.TargetOperationId] = _reservations[ev.TargetOperationId] with
                {
                    State = TypeIIRangeReservationState.Active,
                    SettledFact = ev,
                };
                if (ev.ReleasedOperationId is { } released)
                    _reservations[released] = _reservations[released] with
                    {
                        State = TypeIIRangeReservationState.Released,
                        SettledFact = ev,
                    };
                break;
        }
    }

    RequestError? Check(Uuid operationId, Uuid periodId, ObservationRange newRange, Uuid? priorOperationId)
    {
        if (_reservations.ContainsKey(operationId))
            return new RequestError(RequestErrorKind.Conflict,
                "This operation already holds a reservation or a fence on the calendar.");
        if (priorOperationId is { } prior && (_reservations.GetValueOrDefault(prior) is not { IsLive: true } held ||
                                              held.PeriodId != periodId))
            return new RequestError(RequestErrorKind.Conflict,
                "An amendment must replace a live reservation of its own period.");
        return FindOverlap(periodId, newRange) is not null
            ? new RequestError(RequestErrorKind.Conflict,
                "A program cannot reserve overlapping Type II observation periods.")
            : null;
    }

    RequestError? CheckSettlement(Uuid periodId, TypeIIRangeSettlementKind settlement, Uuid target, Uuid? released)
    {
        var existing = _reservations.GetValueOrDefault(target);
        var bad = settlement switch
        {
            TypeIIRangeSettlementKind.Fenced => existing is not null,
            TypeIIRangeSettlementKind.Released => existing is not { IsLive: true } || existing.PeriodId != periodId,
            _ => existing is not { State: TypeIIRangeReservationState.Pending } || existing.PeriodId != periodId ||
                 released is { } other && (_reservations.GetValueOrDefault(other) is not { IsLive: true } old ||
                                           old.PeriodId != periodId || existing.PriorOperationId != other),
        };
        return bad
            ? new RequestError(RequestErrorKind.Conflict, "The settlement does not match a live reservation of its period.")
            : null;
    }
}
