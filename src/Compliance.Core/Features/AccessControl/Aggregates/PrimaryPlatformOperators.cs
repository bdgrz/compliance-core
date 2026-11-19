using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Aggregates;

/// <summary>Protects at most two current platform-primary seats, never an operator roster.</summary>
/// <remarks>
/// Callers must independently verify the exact committed target outcome, its current protected
/// state where required, actor admission and qualified conditional writes. Event identifiers or
/// attached objects alone are not source authority. Initial confirmation does not prove that
/// configured-user enumeration or bootstrap completion has finished; those are separate pending
/// choreography. The bootstrap plan ID names one qualified immutable configuration generation;
/// it is not a manifest, operator list or aggregate containing users, and this owner never
/// enumerates configuration. This owner keeps two current seats and the selected operation only.
/// </remarks>
public sealed class PrimaryPlatformOperators : Aggregate
{
    readonly Uuid _selectedOperationId;
    Seat? _first;
    Seat? _second;
    Uuid _lineage;
    Uuid _bootstrapPlan;
    ulong _lastGeneration;
    bool _everConfirmed;
    bool _bootstrapComplete;
    DomainEvent? _selectedCommand;
    DomainEvent? _selectedOutcome;

    /// <summary>Opens the platform owner and optionally selects one operation for exact retry.</summary>
    public PrimaryPlatformOperators(Uuid selectedOperationId = default) : base(OwnerId, Address)
    {
        _selectedOperationId = selectedOperationId;
        On<PrimaryPlatformOperatorSeatReserved>(Apply);
        On<PrimaryPlatformOperatorAppointed>(Apply);
        On<PrimaryPlatformOperatorSeatRefused>(Apply);
        On<PrimaryPlatformOperatorSeatReleased>(Apply);
        On<PrimaryPlatformOperatorSeatVacated>(Apply);
        On<PlatformOperatorBootstrapCompleted>(Apply);
    }

    /// <summary>Stable identity of the platform primary owner.</summary>
    public static Uuid OwnerId => AggregateIdentity.Create(nameof(PrimaryPlatformOperators), "platform");
    /// <summary>Exact address of the platform primary owner.</summary>
    public static EventStreamAddress Address => new("bdgrz", "primary-platform-operators", OwnerId.ToString());
    /// <summary>The number of occupied seats, including pending release cleanup.</summary>
    public int OccupiedSeats => (_first is null ? 0 : 1) + (_second is null ? 0 : 1);
    /// <summary>The number of confirmed and unreleased primaries.</summary>
    public int ConfirmedSeats => (_first?.State == "confirmed" ? 1 : 0) + (_second?.State == "confirmed" ? 1 : 0);
    /// <summary>Whether an initial primary has ever been confirmed; this is not bootstrap completion.</summary>
    public bool HasConfirmedInitialPrimary => _everConfirmed;
    /// <summary>The scalar identity of the trusted genesis configuration.</summary>
    public Uuid BootstrapPlanId => _bootstrapPlan;
    /// <summary>Whether genesis configuration processing has completed.</summary>
    public bool IsBootstrapComplete => _bootstrapComplete;
    /// <summary>Highest allocated primary generation in this lineage.</summary>
    public ulong LastGeneration => _lastGeneration;
    /// <summary>The selected reservation or release fact, when present.</summary>
    public DomainEvent? SelectedCommand => _selectedCommand;
    /// <summary>The selected participant outcome, when present.</summary>
    public DomainEvent? SelectedOutcome => _selectedOutcome;

    /// <summary>Looks up one user's current bounded seat, including pending cleanup.</summary>
    public bool TryGetSeat(Uuid userId, out ulong generation, out string? status)
    {
        var seat = FindUser(userId);
        generation = seat?.Generation ?? 0;
        status = seat?.State;
        return seat is not null;
    }

    /// <summary>Records completion after the bootstrap service has processed its fixed configuration.</summary>
    public Result CompleteBootstrap(Uuid bootstrapPlanId)
    {
        if (bootstrapPlanId == Uuid.Empty || bootstrapPlanId != _bootstrapPlan || !_everConfirmed || ConfirmedSeats == 0)
            return Conflict("The exact initialized bootstrap plan and a confirmed primary are required.");
        if (_bootstrapComplete)
            return Result.Success;
        var fact = new PlatformOperatorBootstrapCompleted(bootstrapPlanId);
        if (!PlatformOperatorContentBounds.Payload(fact))
            return PayloadTooLarge();
        RaiseEvent(fact);
        return Result.Success;
    }

    /// <summary>Reserves the initial primary from an independently qualified immutable bootstrap plan.</summary>
    public Result ReserveInitial(Uuid operationId, ulong expectedRevision, Uuid userId, Uuid grantEpisodeId,
        Uuid lineageId, Uuid bootstrapPlanId, ActorAttribution actor) =>
        Reserve(operationId, expectedRevision, userId, grantEpisodeId, lineageId, true, bootstrapPlanId, Uuid.Empty, actor, false);

    /// <summary>Reserves the first seat for an already granted development operator.</summary>
    /// <remarks>The caller must enforce the configured development-only genesis rule.</remarks>
    public Result ReserveInitialExisting(Uuid operationId, ulong expectedRevision, Uuid userId, Uuid grantEpisodeId,
        Uuid lineageId, Uuid bootstrapPlanId, Uuid actorUserId, ActorAttribution actor) =>
        Reserve(operationId, expectedRevision, userId, grantEpisodeId, lineageId, true, bootstrapPlanId, actorUserId, actor, true);

    /// <summary>Reserves an additional exact operator grant after initial primary confirmation.</summary>
    public Result ReservePrimary(Uuid operationId, ulong expectedRevision, Uuid userId, Uuid grantEpisodeId,
        Uuid actorUserId, ActorAttribution actor) =>
        Reserve(operationId, expectedRevision, userId, grantEpisodeId, _lineage, false, Uuid.Empty, actorUserId, actor, false);

    Result Reserve(Uuid operationId, ulong expectedRevision, Uuid userId, Uuid grantEpisodeId, Uuid lineageId,
        bool bootstrap, Uuid bootstrapPlanId, Uuid actorUserId, ActorAttribution actor, bool initialExisting)
    {
        if (!Selected(operationId) || userId == Uuid.Empty || grantEpisodeId == Uuid.Empty || lineageId == Uuid.Empty ||
            (bootstrap ? bootstrapPlanId == Uuid.Empty || (initialExisting ? actorUserId != userId || !HumanActor(actorUserId, actor) :
                actorUserId != Uuid.Empty || !SystemActor(actor)) :
                bootstrapPlanId != Uuid.Empty || !HumanActor(actorUserId, actor)))
            return Invalid();
        if (_selectedCommand is { } selected)
            return selected is PrimaryPlatformOperatorSeatReserved prior && prior.OperationId == operationId &&
                prior.ExpectedRevision == expectedRevision && prior.UserId == userId && prior.GrantEpisodeId == grantEpisodeId &&
                prior.LineageId == lineageId && prior.Bootstrap == bootstrap && prior.BootstrapPlanId == bootstrapPlanId &&
                prior.ActorUserId == actorUserId && prior.Actor == actor && prior.InitialExistingGrant == initialExisting ? Result.Success : ChangedIntent();
        if (Version != expectedRevision || OccupiedSeats >= 2 || FindUser(userId) is not null ||
            _lastGeneration == ulong.MaxValue || (_lineage != Uuid.Empty && _lineage != lineageId) ||
            (bootstrap ? _everConfirmed || OccupiedSeats != 0 || _bootstrapPlan != Uuid.Empty && _bootstrapPlan != bootstrapPlanId :
                !_everConfirmed || ConfirmedSeats == 0))
            return Conflict("The primary seat, exact lineage and current owner revision are required.");
        var fact = new PrimaryPlatformOperatorSeatReserved(operationId, expectedRevision, userId, grantEpisodeId,
            lineageId, _lastGeneration + 1, bootstrap, bootstrapPlanId, actorUserId, actor)
        { InitialExistingGrant = initialExisting };
        if (!PlatformOperatorContentBounds.Payload(fact))
            return PayloadTooLarge();
        RaiseEvent(fact);
        return Result.Success;
    }

    /// <summary>Records one exact source-verified target mark acceptance or durable mark fence.</summary>
    public Result RecordTargetOutcome(DomainEvent target)
    {
        if (!TryTarget(target, out var id, out var operation, out var user, out var grant, out var lineage,
                out var generation, out var reservation, out var accepted, out var bootstrap) || !Selected(operation))
            return Invalid();
        if (_selectedOutcome is { } selected)
        {
            if (selected is PrimaryPlatformOperatorAppointed appointed && accepted && appointed.TargetEventId == id &&
                Matches(appointed.OperationId, appointed.UserId, appointed.GrantEpisodeId, appointed.LineageId, appointed.Generation,
                    appointed.ReservationEventId, operation, user, grant, lineage, generation, reservation))
                return Result.Success;
            if (selected is PrimaryPlatformOperatorSeatRefused refused && !accepted && refused.TargetEventId == id &&
                Matches(refused.OperationId, refused.UserId, refused.GrantEpisodeId, refused.LineageId, refused.Generation,
                    refused.ReservationEventId, operation, user, grant, lineage, generation, reservation))
                return Result.Success;
            if (operation == _selectedOperationId)
                return ChangedIntent();
        }
        var seat = FindOperation(operation);
        if (seat is null || seat.State != "pending" || seat.UserId != user || seat.Grant != grant ||
            seat.Lineage != lineage || seat.Generation != generation || seat.Reservation != reservation ||
            accepted && seat.Bootstrap != bootstrap)
            return Conflict("The exact pending primary reservation is required.");
        DomainEvent fact = accepted
            ? new PrimaryPlatformOperatorAppointed(operation, user, grant, lineage, generation, reservation, id)
            : new PrimaryPlatformOperatorSeatRefused(operation, user, grant, lineage, generation, reservation, id);
        if (!PlatformOperatorContentBounds.Payload(fact))
            return PayloadTooLarge();
        RaiseEvent(fact);
        return Result.Success;
    }

    /// <summary>Releases one confirmed primary only while a different confirmed primary remains.</summary>
    public Result Release(Uuid operationId, ulong expectedRevision, Uuid userId, ulong generation,
        Uuid actorUserId, ActorAttribution actor, string reason)
    {
        if (!Selected(operationId) || userId == Uuid.Empty || generation == 0 || !HumanActor(actorUserId, actor) ||
            !PlatformOperatorContentBounds.Reason(reason))
            return Invalid();
        var canonical = reason.Trim();
        if (_selectedCommand is { } selected)
            return selected is PrimaryPlatformOperatorSeatReleased prior && prior.OperationId == operationId &&
                prior.ExpectedRevision == expectedRevision && prior.UserId == userId && prior.Generation == generation &&
                prior.ActorUserId == actorUserId && prior.Actor == actor && prior.Reason == canonical
                ? Result.Success : ChangedIntent();
        var seat = FindUser(userId);
        var other = Other(seat);
        if (Version != expectedRevision || seat is null || seat.State != "confirmed" || seat.Generation != generation ||
            other is null || other.State != "confirmed")
            return Conflict("A different confirmed replacement primary is required.");
        var fact = new PrimaryPlatformOperatorSeatReleased(operationId, expectedRevision, seat.UserId, seat.Grant,
            seat.Lineage, seat.Generation, seat.Reservation, other.UserId, other.Generation, actorUserId, actor, canonical);
        if (!PlatformOperatorContentBounds.Payload(fact))
            return PayloadTooLarge();
        RaiseEvent(fact);
        return Result.Success;
    }

    /// <summary>Frees a released seat only after independently verifying its exact target unmark.</summary>
    public Result RecordTargetUnmark(PlatformOperatorPrimaryUnmarked target)
    {
        if (target is null || !Selected(target.OperationId) || !TargetMetadata(target, target.UserId, out var id))
            return Invalid();
        if (_selectedOutcome is PrimaryPlatformOperatorSeatVacated prior && prior.OperationId == target.OperationId)
            return prior.TargetEventId == id && prior.ReleaseEventId == target.ReleaseEventId &&
                Matches(prior.OperationId, prior.UserId, prior.GrantEpisodeId, prior.LineageId, prior.Generation,
                    prior.ReservationEventId, target.OperationId, target.UserId, target.GrantEpisodeId, target.LineageId,
                    target.Generation, target.ReservationEventId) ? Result.Success : ChangedIntent();
        var seat = FindUser(target.UserId);
        if (seat is null || seat.State != "releasing" || seat.ReleaseOperation != target.OperationId ||
            seat.ReleaseEvent != target.ReleaseEventId || seat.Grant != target.GrantEpisodeId ||
            seat.Lineage != target.LineageId || seat.Generation != target.Generation || seat.Reservation != target.ReservationEventId)
            return Conflict("The exact released primary and target unmark are required.");
        var fact = new PrimaryPlatformOperatorSeatVacated(target.OperationId, target.UserId, target.GrantEpisodeId,
            target.LineageId, target.Generation, target.ReservationEventId, target.ReleaseEventId, id);
        if (!PlatformOperatorContentBounds.Payload(fact))
            return PayloadTooLarge();
        RaiseEvent(fact);
        return Result.Success;
    }

    void Apply(PrimaryPlatformOperatorSeatReserved ev)
    {
        Require(ev.ExpectedRevision == Version && ev.Metadata.EventId != Uuid.Empty && ev.OperationId != Uuid.Empty && ev.UserId != Uuid.Empty &&
            ev.GrantEpisodeId != Uuid.Empty && ev.LineageId != Uuid.Empty && ev.Generation > 0 &&
            _lastGeneration != ulong.MaxValue && ev.Generation == _lastGeneration + 1 && OccupiedSeats < 2 &&
            FindUser(ev.UserId) is null && (_lineage == Uuid.Empty || _lineage == ev.LineageId) &&
            (ev.Bootstrap ? !_everConfirmed && OccupiedSeats == 0 && ev.BootstrapPlanId != Uuid.Empty &&
                (_bootstrapPlan == Uuid.Empty || _bootstrapPlan == ev.BootstrapPlanId) &&
                (ev.InitialExistingGrant ? ev.ActorUserId == ev.UserId && HumanActor(ev.ActorUserId, ev.Actor) :
                    ev.ActorUserId == Uuid.Empty && SystemActor(ev.Actor)) :
                !ev.InitialExistingGrant && _everConfirmed && ConfirmedSeats > 0 && ev.BootstrapPlanId == Uuid.Empty && HumanActor(ev.ActorUserId, ev.Actor)) &&
            PlatformOperatorContentBounds.Payload(ev));
        _lineage = ev.LineageId;
        _lastGeneration = ev.Generation;
        if (ev.Bootstrap)
            _bootstrapPlan = ev.BootstrapPlanId;
        Put(new Seat(ev.UserId, ev.GrantEpisodeId, ev.LineageId, ev.Generation, ev.OperationId,
            ev.Metadata.EventId, ev.Bootstrap && !ev.InitialExistingGrant, "pending", Uuid.Empty, Uuid.Empty));
        SelectCommand(ev.OperationId, ev);
    }

    void Apply(PrimaryPlatformOperatorAppointed ev)
    {
        var seat = FindOperation(ev.OperationId);
        Require(seat is not null && seat.State == "pending" && ev.TargetEventId != Uuid.Empty &&
            Matches(seat.Operation, seat.UserId, seat.Grant, seat.Lineage, seat.Generation, seat.Reservation,
                ev.OperationId, ev.UserId, ev.GrantEpisodeId, ev.LineageId, ev.Generation, ev.ReservationEventId) &&
            PlatformOperatorContentBounds.Payload(ev));
        Put(seat! with { State = "confirmed" });
        _everConfirmed = true;
        SelectOutcome(ev.OperationId, ev);
    }

    void Apply(PrimaryPlatformOperatorSeatRefused ev)
    {
        var seat = FindOperation(ev.OperationId);
        Require(seat is not null && seat.State == "pending" && ev.TargetEventId != Uuid.Empty &&
            Matches(seat.Operation, seat.UserId, seat.Grant, seat.Lineage, seat.Generation, seat.Reservation,
                ev.OperationId, ev.UserId, ev.GrantEpisodeId, ev.LineageId, ev.Generation, ev.ReservationEventId) &&
            PlatformOperatorContentBounds.Payload(ev));
        Remove(seat!);
        SelectOutcome(ev.OperationId, ev);
    }

    void Apply(PrimaryPlatformOperatorSeatReleased ev)
    {
        var seat = FindUser(ev.UserId);
        var other = Other(seat);
        Require(ev.ExpectedRevision == Version && ev.Metadata.EventId != Uuid.Empty && ev.OperationId != Uuid.Empty && HumanActor(ev.ActorUserId, ev.Actor) &&
            PlatformOperatorContentBounds.CanonicalReason(ev.Reason) && seat is not null && seat.State == "confirmed" &&
            seat.Grant == ev.GrantEpisodeId && seat.Lineage == ev.LineageId && seat.Generation == ev.Generation &&
            seat.Reservation == ev.ReservationEventId && other is not null && other.State == "confirmed" &&
            other.UserId == ev.ReplacementUserId && other.Generation == ev.ReplacementGeneration &&
            PlatformOperatorContentBounds.Payload(ev));
        Put(seat! with { State = "releasing", ReleaseOperation = ev.OperationId, ReleaseEvent = ev.Metadata.EventId });
        SelectCommand(ev.OperationId, ev);
    }

    void Apply(PrimaryPlatformOperatorSeatVacated ev)
    {
        var seat = FindUser(ev.UserId);
        Require(seat is not null && seat.State == "releasing" && ev.TargetEventId != Uuid.Empty &&
            seat.ReleaseOperation == ev.OperationId && seat.ReleaseEvent == ev.ReleaseEventId && seat.Grant == ev.GrantEpisodeId &&
            seat.Lineage == ev.LineageId && seat.Generation == ev.Generation && seat.Reservation == ev.ReservationEventId &&
            ConfirmedSeats >= 1 && PlatformOperatorContentBounds.Payload(ev));
        Remove(seat!);
        SelectOutcome(ev.OperationId, ev);
    }

    void Apply(PlatformOperatorBootstrapCompleted ev)
    {
        Require(!_bootstrapComplete && _everConfirmed && ConfirmedSeats > 0 && ev.BootstrapPlanId != Uuid.Empty &&
            ev.BootstrapPlanId == _bootstrapPlan && PlatformOperatorContentBounds.Payload(ev));
        _bootstrapComplete = true;
    }

    static bool TryTarget(DomainEvent target, out Uuid id, out Uuid operation, out Uuid user, out Uuid grant,
        out Uuid lineage, out ulong generation, out Uuid reservation, out bool accepted, out bool bootstrap)
    {
        id = Uuid.Empty;
        (operation, user, grant, lineage, generation, reservation, accepted, bootstrap) = target switch
        {
            PlatformOperatorPrimaryMarkAccepted e => (e.OperationId, e.UserId, e.GrantEpisodeId, e.LineageId, e.Generation, e.ReservationEventId, true, false),
            PlatformOperatorBootstrapGrantAndMarkAccepted e when SystemActor(e.Actor) =>
                (e.OperationId, e.UserId, e.GrantEpisodeId, e.LineageId, e.Generation, e.ReservationEventId, true, true),
            PlatformOperatorPrimaryMarkFenced e => (e.OperationId, e.UserId, e.GrantEpisodeId, e.LineageId, e.Generation, e.ReservationEventId, false, false),
            _ => (Uuid.Empty, Uuid.Empty, Uuid.Empty, Uuid.Empty, 0UL, Uuid.Empty, false, false),
        };
        return operation != Uuid.Empty && user != Uuid.Empty && grant != Uuid.Empty && lineage != Uuid.Empty &&
            generation > 0 && reservation != Uuid.Empty && TargetMetadata(target, user, out id);
    }

    static bool TargetMetadata(DomainEvent target, Uuid user, out Uuid id)
    {
        id = Uuid.Empty;
        try
        {
            var metadata = target.Metadata;
            var expectedRevision = target switch
            {
                PlatformOperatorPrimaryMarkAccepted e => e.ExpectedRevision,
                PlatformOperatorBootstrapGrantAndMarkAccepted e => e.ExpectedRevision,
                PlatformOperatorPrimaryMarkFenced e => e.ExpectedRevision,
                PlatformOperatorPrimaryUnmarked e => e.ExpectedRevision,
                _ => ulong.MaxValue,
            };
            if (metadata.IsAudit || metadata.EventId == Uuid.Empty || metadata.AggregateVersion == 0 ||
                metadata.AggregateId != PlatformOperator.IdentityOf(user) || expectedRevision == ulong.MaxValue ||
                metadata.AggregateVersion != expectedRevision + 1)
                return false;
            id = metadata.EventId;
            return true;
        }
        catch (InvalidOperationException) { return false; }
    }

    Seat? FindUser(Uuid user) => _first?.UserId == user ? _first : _second?.UserId == user ? _second : null;
    Seat? FindOperation(Uuid operation) => _first?.Operation == operation ? _first : _second?.Operation == operation ? _second : null;
    Seat? Other(Seat? seat) => seat is null ? null : _first == seat ? _second : _second == seat ? _first : null;
    void Put(Seat seat)
    {
        if (_first?.UserId == seat.UserId)
            _first = seat;
        else if (_second?.UserId == seat.UserId)
            _second = seat;
        else if (_first is null)
            _first = seat;
        else
            _second = seat;
    }
    void Remove(Seat seat)
    {
        if (_first == seat)
            _first = null;
        else if (_second == seat)
            _second = null;
        else
            throw new InvalidOperationException("The primary seat is not current.");
    }
    void SelectCommand(Uuid operation, DomainEvent fact) { if (operation == _selectedOperationId) { Require(_selectedCommand is null); _selectedCommand = fact; } }
    void SelectOutcome(Uuid operation, DomainEvent fact) { if (operation == _selectedOperationId) { Require(_selectedOutcome is null); _selectedOutcome = fact; } }
    bool Selected(Uuid operation) => operation != Uuid.Empty && operation == _selectedOperationId;
    static bool SystemActor(ActorAttribution? actor) => actor == new ActorAttribution("portia:system", "Portia");
    static bool HumanActor(Uuid user, ActorAttribution? actor) => user != Uuid.Empty && actor is not null &&
        actor.Subject == user.ToString() && PlatformOperatorContentBounds.ActorFields(actor);
    static bool Matches(Uuid operation, Uuid user, Uuid grant, Uuid lineage, ulong generation, Uuid reservation,
        Uuid otherOperation, Uuid otherUser, Uuid otherGrant, Uuid otherLineage, ulong otherGeneration, Uuid otherReservation) =>
        operation == otherOperation && user == otherUser && grant == otherGrant && lineage == otherLineage &&
        generation == otherGeneration && reservation == otherReservation;
    static void Require(bool condition) { if (!condition) throw new InvalidOperationException("The primary seat fact violates its owner and lifecycle."); }
    static Result Invalid() => Failure(RequestErrorKind.Validation, "The exact selected operation and participant evidence are required.");
    static Result ChangedIntent() => Conflict("The operation identity already names another intent or outcome.");
    static Result Conflict(string message) => Failure(RequestErrorKind.Conflict, message);
    static Result PayloadTooLarge() => Failure(RequestErrorKind.Validation, "A primary-seat JSON business payload must fit within 8192 bytes.");
    static Result Failure(RequestErrorKind kind, string message) => Result.Failure(new RequestError(kind, message));
    sealed record Seat(Uuid UserId, Uuid Grant, Uuid Lineage, ulong Generation, Uuid Operation, Uuid Reservation,
        bool Bootstrap, string State, Uuid ReleaseOperation, Uuid ReleaseEvent);
}
