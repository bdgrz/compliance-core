using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Operations;

/// <summary>Transient live and explicitly selected terminal authority for one range decision.</summary>
public sealed class TypeIICalendarRules
{
    readonly Dictionary<Uuid, TypeIICalendarReservationContent> _reservations = [];
    readonly List<object> _pending = [];
    readonly List<object> _applied = [];
    public TypeIICalendarRules(Uuid tenantId, Uuid programId, long revision = 0, long generation = 0,
        IEnumerable<TypeIICalendarReservationContent>? reservations = null)
    {
        TenantId = tenantId;
        Id = programId;
        Revision = revision;
        ReservationGeneration = generation;
        foreach (var reservation in reservations ?? [])
            _reservations.Add(reservation.OperationId, reservation);
    }
    public Uuid TenantId { get; }
    public Uuid Id { get; }
    public EventStreamAddress Stream => new(TenantId.ToString(), TypeIIObservationCalendar.Area, Id.ToString());
    public long Revision { get; private set; }
    public long ReservationGeneration { get; private set; }
    public IReadOnlyCollection<TypeIICalendarReservationContent> Reservations => _reservations.Values;
    public IReadOnlyList<object> PendingChanges => _pending;
    public IReadOnlyList<object> AppliedChanges => _applied;
    public void ClearPendingChanges() => _pending.Clear();
    public TypeIICalendarReservationContent? Find(Uuid operationId) => _reservations.GetValueOrDefault(operationId);
    public TypeIICalendarReservationContent? FindOverlap(Uuid periodId, ObservationRange range)
    {
        ArgumentNullException.ThrowIfNull(range);
        return _reservations.Values.Where(reservation => reservation.IsLive && reservation.PeriodId != periodId &&
            reservation.Range!.Overlaps(range)).OrderBy(reservation => reservation.Generation).FirstOrDefault();
    }
    public Result Authorize(OperationProvenance operation, ImmutableContentRef intent, Uuid periodId,
        TypeIIRangeOperationKind kind, ObservationRange newRange, Uuid? priorOperationId)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(newRange);
        if (operation.Owner != Stream || operation.RootOperationId == Uuid.Empty || periodId == Uuid.Empty || !newRange.IsValid ||
            kind is not TypeIIRangeOperationKind.Opening and not TypeIIRangeOperationKind.Amendment ||
            kind == TypeIIRangeOperationKind.Opening != (priorOperationId is null))
            return Failure(RequestErrorKind.Validation, "A range authorization names its calendar, period, a valid range and, for an amendment, its prior range.");
        if (_reservations.ContainsKey(operation.RootOperationId))
            return Failure(RequestErrorKind.Conflict, "This operation already holds a reservation or a fence on the calendar.");
        if (priorOperationId is { } prior && (Find(prior) is not { IsLive: true } held || held.PeriodId != periodId))
            return Failure(RequestErrorKind.Conflict, "An amendment must replace a live reservation of its own period.");
        if (FindOverlap(periodId, newRange) is not null)
            return Failure(RequestErrorKind.Conflict, "A program cannot reserve overlapping Type II observation periods.");
        Raise(new TypeIICalendarAuthorizationChange(operation, intent, periodId, kind, newRange, priorOperationId));
        return Result.Success;
    }
    public Result Settle(OperationProvenance operation, ImmutableContentRef intent, Uuid periodId,
        TypeIIRangeSettlementKind settlement, Uuid targetOperationId, Uuid? releasedOperationId)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream || periodId == Uuid.Empty || targetOperationId == Uuid.Empty ||
            releasedOperationId is not null && settlement != TypeIIRangeSettlementKind.Activated)
            return Failure(RequestErrorKind.Validation, "A settlement names its calendar, period and the exact reservation it settles.");
        var existing = Find(targetOperationId);
        var bad = settlement switch
        {
            TypeIIRangeSettlementKind.Fenced => existing is not null,
            TypeIIRangeSettlementKind.Released => existing is not { IsLive: true } || existing.PeriodId != periodId,
            TypeIIRangeSettlementKind.Activated => existing is not { State: TypeIIRangeReservationState.Pending } || existing.PeriodId != periodId ||
                releasedOperationId is { } other && (Find(other) is not { IsLive: true } old || old.PeriodId != periodId || existing.PriorOperationId != other),
            _ => true,
        };
        if (bad)
            return Failure(RequestErrorKind.Conflict, "The settlement does not match a live reservation of its period.");
        Raise(new TypeIICalendarSettlementChange(operation, intent, periodId, settlement, targetOperationId, releasedOperationId));
        return Result.Success;
    }
    void Raise(object change) { Apply(change); _pending.Add(change); }
    public void Apply(object change)
    {
        Revision++;
        switch (change)
        {
            case TypeIICalendarAuthorizationChange authorization:
                ReservationGeneration++;
                _reservations.Add(authorization.Operation.RootOperationId, new(authorization.Operation.RootOperationId, authorization.PeriodId,
                    TypeIIRangeReservationState.Pending, authorization.Kind, authorization.NewRange, ReservationGeneration,
                    authorization.PriorOperationId, Revision, null)
                { AuthorizationBindingSha256 = TypeIICalendarContent.Binding(authorization) });
                break;
            case TypeIICalendarSettlementChange { Settlement: TypeIIRangeSettlementKind.Fenced } settlement:
                _reservations.Add(settlement.TargetOperationId, new(settlement.TargetOperationId, settlement.PeriodId,
                    TypeIIRangeReservationState.Fenced, TypeIIRangeOperationKind.Opening, null, 0, null, null, Revision)
                { SettlementBindingSha256 = TypeIICalendarContent.Binding(settlement) });
                break;
            case TypeIICalendarSettlementChange settlement:
                _reservations[settlement.TargetOperationId] = _reservations[settlement.TargetOperationId] with
                {
                    State = settlement.Settlement == TypeIIRangeSettlementKind.Released ? TypeIIRangeReservationState.Released : TypeIIRangeReservationState.Active,
                    SettlementRevision = Revision,
                    SettlementBindingSha256 = TypeIICalendarContent.Binding(settlement),
                };
                if (settlement.ReleasedOperationId is { } released)
                    _reservations[released] = _reservations[released] with { State = TypeIIRangeReservationState.Released, SettlementRevision = Revision, SettlementBindingSha256 = TypeIICalendarContent.Binding(settlement) };
                break;
            default:
                throw new InvalidOperationException("Another calendar mutation kind.");
        }
        _applied.Add(change);
    }
    static Result Failure(RequestErrorKind kind, string message) => Result.Failure(new RequestError(kind, message));
}
