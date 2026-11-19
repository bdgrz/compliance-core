using Bdgrz.Compliance.Features.AccessControl.Events;
using Bdgrz.Compliance.Features.AccessControl.Rules;
using Bdgrz.Compliance.Features.Kernel.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl.Aggregates;

/// <summary>Owns one user's operator grant and its current protected-primary mark.</summary>
/// <remarks>
/// The caller must independently qualify human admission or the exact committed seat reservation
/// or release before invoking a transition. Source reads and cross-owner authority are not
/// implied by identifiers passed here. Every write must use a qualified target head and exact
/// conditional append; this owner does not enforce seat count or bootstrap/cutover policy.
/// Only the selected historical operation is retained, never a roster or operation history.
/// </remarks>
public sealed class PlatformOperator : Aggregate
{
    readonly Uuid _userId;
    readonly Uuid _selectedOperationId;
    DomainEvent? _selectedOutcome;
    Uuid _grantEpisodeId;
    bool _everGranted;
    Uuid _primaryLineageId;
    ulong _highestGeneration;
    ulong _primaryGeneration;
    Uuid _reservationEventId;

    /// <summary>Opens one user and optionally selects one operation for exact retry.</summary>
    public PlatformOperator(Uuid userId, Uuid selectedOperationId = default)
        : base(IdentityOf(userId), AddressOf(userId))
    {
        if (userId == Uuid.Empty)
            throw new ArgumentException("A user identity is required.", nameof(userId));
        _userId = userId;
        _selectedOperationId = selectedOperationId;
        On<PlatformOperatorAccessGranted>(Apply);
        On<PlatformOperatorAccessRevoked>(Apply);
        On<PlatformOperatorBootstrapGrantAndMarkAccepted>(Apply);
        On<PlatformOperatorBootstrapAccessGranted>(Apply);
        On<PlatformOperatorPrimaryMarkAccepted>(Apply);
        On<PlatformOperatorPrimaryMarkFenced>(Apply);
        On<PlatformOperatorPrimaryUnmarked>(Apply);
    }

    /// <summary>Creates this user's deterministic owner identity.</summary>
    public static Uuid IdentityOf(Uuid userId) => AggregateIdentity.Create(nameof(PlatformOperator), userId);
    /// <summary>Creates this user's exact source address.</summary>
    public static EventStreamAddress AddressOf(Uuid userId) =>
        new("bdgrz", "platform-operators", IdentityOf(userId).ToString());
    /// <summary>Whether the current operator grant is active.</summary>
    public bool IsActive => _grantEpisodeId != Uuid.Empty;
    /// <summary>The exact current grant episode, or empty when inactive.</summary>
    public Uuid GrantEpisodeId => _grantEpisodeId;
    /// <summary>Whether the current grant is protected as a primary.</summary>
    public bool IsPrimary => _primaryGeneration != 0;
    /// <summary>The current primary lineage, or empty before the first designation decision.</summary>
    public Uuid PrimaryLineageId => _primaryLineageId;
    /// <summary>The highest target-decided primary generation in the current lineage.</summary>
    public ulong HighestPrimaryGeneration => _highestGeneration;
    /// <summary>The current marked primary generation, or zero when unmarked.</summary>
    public ulong PrimaryGeneration => _primaryGeneration;
    /// <summary>The reservation that produced the current mark, or empty when unmarked.</summary>
    public Uuid PrimaryReservationEventId => _reservationEventId;
    /// <summary>The selected operation's exact historical outcome, when present.</summary>
    public DomainEvent? SelectedOutcome => _selectedOutcome;

    /// <summary>Grants one user access under an externally qualified human admission.</summary>
    public Result Grant(Uuid operationId, ulong expectedRevision, Uuid actorUserId, ActorAttribution actor, string reason)
    {
        if (!ValidHuman(operationId, actorUserId, actor, reason))
            return Invalid();
        var canonical = reason.Trim();
        if (_selectedOutcome is { } selected)
            return selected is PlatformOperatorAccessGranted prior && prior.UserId == _userId && prior.OperationId == operationId &&
                   prior.ExpectedRevision == expectedRevision && prior.ActorUserId == actorUserId && prior.Actor == actor && prior.Reason == canonical
                ? Result.Success : ChangedIntent();
        if (Version != expectedRevision || IsActive)
            return Conflict("The exact inactive operator revision is required.");
        var fact = new PlatformOperatorAccessGranted(_userId, operationId, expectedRevision, actorUserId, actor, canonical);
        if (!PlatformOperatorContentBounds.Payload(fact))
            return PayloadTooLarge();
        RaiseEvent(fact);
        return Result.Success;
    }

    /// <summary>Revokes an exact unprotected grant under externally qualified human admission.</summary>
    public Result Revoke(Uuid operationId, ulong expectedRevision, Uuid grantEpisodeId, Uuid actorUserId,
        ActorAttribution actor, string reason)
    {
        if (!ValidHuman(operationId, actorUserId, actor, reason) || grantEpisodeId == Uuid.Empty)
            return Invalid();
        var canonical = reason.Trim();
        if (_selectedOutcome is { } selected)
            return selected is PlatformOperatorAccessRevoked prior && prior.UserId == _userId && prior.OperationId == operationId &&
                   prior.ExpectedRevision == expectedRevision && prior.GrantEpisodeId == grantEpisodeId && prior.ActorUserId == actorUserId &&
                   prior.Actor == actor && prior.Reason == canonical ? Result.Success : ChangedIntent();
        if (Version != expectedRevision || !IsActive || _grantEpisodeId != grantEpisodeId || IsPrimary)
            return Conflict("The exact active grant must be unmarked before revocation.");
        var fact = new PlatformOperatorAccessRevoked(_userId, operationId, expectedRevision, grantEpisodeId, actorUserId, actor, canonical);
        if (!PlatformOperatorContentBounds.Payload(fact))
            return PayloadTooLarge();
        RaiseEvent(fact);
        return Result.Success;
    }

    /// <summary>Atomically accepts an initial grant and primary mark from a source-verified bootstrap reservation.</summary>
    public Result AcceptBootstrap(Uuid operationId, ulong expectedRevision, Uuid grantEpisodeId, Uuid lineageId,
        ulong generation, Uuid reservationEventId, ActorAttribution actor)
    {
        if (!ValidMark(operationId, grantEpisodeId, lineageId, generation, reservationEventId) ||
            actor != new ActorAttribution("portia:system", "Portia"))
            return Invalid();
        if (_selectedOutcome is { } selected)
            return selected is PlatformOperatorBootstrapGrantAndMarkAccepted prior && prior.UserId == _userId &&
                   prior.OperationId == operationId && prior.ExpectedRevision == expectedRevision &&
                   prior.GrantEpisodeId == grantEpisodeId && prior.LineageId == lineageId && prior.Generation == generation &&
                   prior.ReservationEventId == reservationEventId && prior.Actor == actor ? Result.Success : ChangedIntent();
        if (Version != expectedRevision || _everGranted || IsActive || IsPrimary || !SameLineage(lineageId) ||
            generation <= _highestGeneration)
            return Conflict("Bootstrap requires a never-granted target and a new unfenced generation.");
        var fact = new PlatformOperatorBootstrapGrantAndMarkAccepted(_userId, operationId, expectedRevision,
            grantEpisodeId, lineageId, generation, reservationEventId, actor);
        if (!PlatformOperatorContentBounds.Payload(fact))
            return PayloadTooLarge();
        RaiseEvent(fact);
        return Result.Success;
    }

    /// <summary>Grants one configured user exactly once under the trusted bootstrap plan.</summary>
    public Result GrantBootstrap(Uuid operationId, ulong expectedRevision, Uuid bootstrapPlanId, ActorAttribution actor)
    {
        if (!Selected(operationId) || bootstrapPlanId == Uuid.Empty || actor != new ActorAttribution("portia:system", "Portia"))
            return Invalid();
        if (_selectedOutcome is { } selected)
            return selected is PlatformOperatorBootstrapAccessGranted prior && prior.UserId == _userId &&
                prior.OperationId == operationId && prior.ExpectedRevision == expectedRevision &&
                prior.BootstrapPlanId == bootstrapPlanId && prior.Actor == actor ? Result.Success : ChangedIntent();
        if (Version != expectedRevision || _everGranted || IsActive || IsPrimary)
            return Conflict("Bootstrap requires a never-granted target at the expected revision.");
        var fact = new PlatformOperatorBootstrapAccessGranted(_userId, operationId, expectedRevision, bootstrapPlanId, actor);
        if (!PlatformOperatorContentBounds.Payload(fact))
            return PayloadTooLarge();
        RaiseEvent(fact);
        return Result.Success;
    }

    /// <summary>Accepts one exact source-verified reservation for the current grant.</summary>
    public Result AcceptPrimaryMark(Uuid operationId, ulong expectedRevision, Uuid grantEpisodeId,
        Uuid lineageId, ulong generation, Uuid reservationEventId)
    {
        if (!ValidMark(operationId, grantEpisodeId, lineageId, generation, reservationEventId))
            return Invalid();
        if (_selectedOutcome is { } selected)
            return selected is PlatformOperatorPrimaryMarkAccepted prior && Matches(prior.UserId, prior.OperationId,
                prior.ExpectedRevision, prior.GrantEpisodeId, prior.LineageId, prior.Generation, prior.ReservationEventId,
                operationId, expectedRevision, grantEpisodeId, lineageId, generation, reservationEventId) ? Result.Success : ChangedIntent();
        if (Version != expectedRevision || !IsActive || _grantEpisodeId != grantEpisodeId || IsPrimary ||
            !SameLineage(lineageId) || generation <= _highestGeneration)
            return Conflict("The exact current unmarked grant and a new designation generation are required.");
        var fact = new PlatformOperatorPrimaryMarkAccepted(_userId, operationId, expectedRevision,
            grantEpisodeId, lineageId, generation, reservationEventId);
        if (!PlatformOperatorContentBounds.Payload(fact))
            return PayloadTooLarge();
        RaiseEvent(fact);
        return Result.Success;
    }

    /// <summary>Fences one exact source-verified reservation against delayed target marks.</summary>
    public Result FencePrimaryMark(Uuid operationId, ulong expectedRevision, Uuid grantEpisodeId,
        Uuid lineageId, ulong generation, Uuid reservationEventId)
    {
        if (!ValidMark(operationId, grantEpisodeId, lineageId, generation, reservationEventId))
            return Invalid();
        if (_selectedOutcome is { } selected)
            return selected is PlatformOperatorPrimaryMarkFenced prior && Matches(prior.UserId, prior.OperationId,
                prior.ExpectedRevision, prior.GrantEpisodeId, prior.LineageId, prior.Generation, prior.ReservationEventId,
                operationId, expectedRevision, grantEpisodeId, lineageId, generation, reservationEventId) ? Result.Success : ChangedIntent();
        if (Version != expectedRevision || !SameLineage(lineageId) || IsPrimary && _primaryGeneration == generation)
            return Conflict("The current target cannot fence an accepted primary mark.");
        var fact = new PlatformOperatorPrimaryMarkFenced(_userId, operationId, expectedRevision,
            grantEpisodeId, lineageId, generation, reservationEventId);
        if (!PlatformOperatorContentBounds.Payload(fact))
            return PayloadTooLarge();
        RaiseEvent(fact);
        return Result.Success;
    }

    /// <summary>Removes the exact current primary mark after source verification of its committed seat release.</summary>
    public Result RemovePrimaryMark(Uuid operationId, ulong expectedRevision, Uuid grantEpisodeId, Uuid lineageId,
        ulong generation, Uuid reservationEventId, Uuid releaseEventId)
    {
        if (!ValidMark(operationId, grantEpisodeId, lineageId, generation, reservationEventId) || releaseEventId == Uuid.Empty)
            return Invalid();
        if (_selectedOutcome is { } selected)
            return selected is PlatformOperatorPrimaryUnmarked prior && Matches(prior.UserId, prior.OperationId,
                prior.ExpectedRevision, prior.GrantEpisodeId, prior.LineageId, prior.Generation, prior.ReservationEventId,
                operationId, expectedRevision, grantEpisodeId, lineageId, generation, reservationEventId) &&
                prior.ReleaseEventId == releaseEventId ? Result.Success : ChangedIntent();
        if (Version != expectedRevision || !IsActive || _grantEpisodeId != grantEpisodeId || _primaryLineageId != lineageId ||
            _primaryGeneration != generation || _reservationEventId != reservationEventId)
            return Conflict("The exact current primary mark and committed seat release are required.");
        var fact = new PlatformOperatorPrimaryUnmarked(_userId, operationId, expectedRevision, grantEpisodeId,
            lineageId, generation, reservationEventId, releaseEventId);
        if (!PlatformOperatorContentBounds.Payload(fact))
            return PayloadTooLarge();
        RaiseEvent(fact);
        return Result.Success;
    }

    void Apply(PlatformOperatorAccessGranted ev)
    {
        Require(ev.UserId == _userId && ev.ExpectedRevision == Version && ev.OperationId != Uuid.Empty &&
            ValidActor(ev.ActorUserId, ev.Actor) && PlatformOperatorContentBounds.CanonicalReason(ev.Reason) &&
            PlatformOperatorContentBounds.Payload(ev) && !IsActive);
        _grantEpisodeId = ev.OperationId;
        _everGranted = true;
        Select(ev.OperationId, ev);
    }

    void Apply(PlatformOperatorAccessRevoked ev)
    {
        Require(ev.UserId == _userId && ev.ExpectedRevision == Version && ev.OperationId != Uuid.Empty &&
            ValidActor(ev.ActorUserId, ev.Actor) && PlatformOperatorContentBounds.CanonicalReason(ev.Reason) && PlatformOperatorContentBounds.Payload(ev) &&
            IsActive && _grantEpisodeId == ev.GrantEpisodeId && !IsPrimary);
        _grantEpisodeId = Uuid.Empty;
        Select(ev.OperationId, ev);
    }

    void Apply(PlatformOperatorBootstrapGrantAndMarkAccepted ev)
    {
        Require(ev.UserId == _userId && ev.ExpectedRevision == Version && !_everGranted && !IsActive && !IsPrimary &&
            Shape(ev.OperationId, ev.GrantEpisodeId, ev.LineageId, ev.Generation, ev.ReservationEventId) &&
            SameLineage(ev.LineageId) && ev.Generation > _highestGeneration &&
            ev.Actor == new ActorAttribution("portia:system", "Portia") && PlatformOperatorContentBounds.Payload(ev));
        _grantEpisodeId = ev.GrantEpisodeId;
        _everGranted = true;
        SetMark(ev.LineageId, ev.Generation, ev.ReservationEventId);
        Select(ev.OperationId, ev);
    }

    void Apply(PlatformOperatorBootstrapAccessGranted ev)
    {
        Require(ev.UserId == _userId && ev.ExpectedRevision == Version && ev.OperationId != Uuid.Empty &&
            ev.BootstrapPlanId != Uuid.Empty && ev.Actor == new ActorAttribution("portia:system", "Portia") &&
            !_everGranted && !IsActive && !IsPrimary && PlatformOperatorContentBounds.Payload(ev));
        _grantEpisodeId = ev.OperationId;
        _everGranted = true;
        Select(ev.OperationId, ev);
    }

    void Apply(PlatformOperatorPrimaryMarkAccepted ev)
    {
        Require(ev.UserId == _userId && ev.ExpectedRevision == Version &&
            Shape(ev.OperationId, ev.GrantEpisodeId, ev.LineageId, ev.Generation, ev.ReservationEventId) &&
            IsActive && _grantEpisodeId == ev.GrantEpisodeId && !IsPrimary && SameLineage(ev.LineageId) &&
            ev.Generation > _highestGeneration && PlatformOperatorContentBounds.Payload(ev));
        SetMark(ev.LineageId, ev.Generation, ev.ReservationEventId);
        Select(ev.OperationId, ev);
    }

    void Apply(PlatformOperatorPrimaryMarkFenced ev)
    {
        Require(ev.UserId == _userId && ev.ExpectedRevision == Version &&
            Shape(ev.OperationId, ev.GrantEpisodeId, ev.LineageId, ev.Generation, ev.ReservationEventId) && SameLineage(ev.LineageId) &&
            (!IsPrimary || _primaryGeneration != ev.Generation) && PlatformOperatorContentBounds.Payload(ev));
        _primaryLineageId = ev.LineageId;
        _highestGeneration = Math.Max(_highestGeneration, ev.Generation);
        Select(ev.OperationId, ev);
    }

    void Apply(PlatformOperatorPrimaryUnmarked ev)
    {
        Require(ev.UserId == _userId && ev.ExpectedRevision == Version && ev.ReleaseEventId != Uuid.Empty &&
            Shape(ev.OperationId, ev.GrantEpisodeId, ev.LineageId, ev.Generation, ev.ReservationEventId) && IsActive &&
            _grantEpisodeId == ev.GrantEpisodeId && _primaryLineageId == ev.LineageId &&
            _primaryGeneration == ev.Generation && _reservationEventId == ev.ReservationEventId && PlatformOperatorContentBounds.Payload(ev));
        _primaryGeneration = 0;
        _reservationEventId = Uuid.Empty;
        Select(ev.OperationId, ev);
    }

    bool ValidHuman(Uuid operationId, Uuid actorUserId, ActorAttribution actor, string reason) =>
        Selected(operationId) && ValidActor(actorUserId, actor) && PlatformOperatorContentBounds.Reason(reason);
    bool ValidMark(Uuid operationId, Uuid grantEpisodeId, Uuid lineageId, ulong generation, Uuid reservationEventId) =>
        Selected(operationId) && Shape(operationId, grantEpisodeId, lineageId, generation, reservationEventId);
    static bool Shape(Uuid operationId, Uuid grantEpisodeId, Uuid lineageId, ulong generation, Uuid reservationEventId) =>
        operationId != Uuid.Empty && grantEpisodeId != Uuid.Empty && lineageId != Uuid.Empty && generation > 0 && reservationEventId != Uuid.Empty;
    static bool ValidActor(Uuid userId, ActorAttribution? actor) => userId != Uuid.Empty && actor is not null &&
        actor.Subject == userId.ToString() && PlatformOperatorContentBounds.ActorFields(actor);
    bool Selected(Uuid operationId) => operationId != Uuid.Empty && operationId == _selectedOperationId;
    bool SameLineage(Uuid lineageId) => _primaryLineageId == Uuid.Empty || _primaryLineageId == lineageId;
    void SetMark(Uuid lineageId, ulong generation, Uuid reservationEventId)
    {
        _primaryLineageId = lineageId;
        _highestGeneration = generation;
        _primaryGeneration = generation;
        _reservationEventId = reservationEventId;
    }
    void Select(Uuid operationId, DomainEvent outcome)
    {
        if (operationId != _selectedOperationId)
            return;
        Require(_selectedOutcome is null);
        _selectedOutcome = outcome;
    }
    bool Matches(Uuid userId, Uuid priorOperation, ulong priorRevision, Uuid priorGrant, Uuid priorLineage,
        ulong priorGeneration, Uuid priorReservation, Uuid operation, ulong revision, Uuid grant, Uuid lineage,
        ulong generation, Uuid reservation) => userId == _userId && priorOperation == operation && priorRevision == revision &&
        priorGrant == grant && priorLineage == lineage && priorGeneration == generation && priorReservation == reservation;
    static void Require(bool condition)
    {
        if (!condition)
            throw new InvalidOperationException("The platform operator fact does not match its owner and lifecycle.");
    }
    static Result Invalid() => Failure(RequestErrorKind.Validation, "The selected operation and exact bounded intent are required.");
    static Result PayloadTooLarge() => Failure(RequestErrorKind.Validation, "A platform operator event's JSON business payload must fit within 8192 bytes.");
    static Result ChangedIntent() => Failure(RequestErrorKind.Conflict, "The operation identity is already bound to different intent.");
    static Result Conflict(string message) => Failure(RequestErrorKind.Conflict, message);
    static Result Failure(RequestErrorKind kind, string message) => Result.Failure(new RequestError(kind, message));
}
