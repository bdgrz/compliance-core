using Bdgrz.Compliance.Features.Criteria.Events;
using Bdgrz.Compliance.Features.Kernel.Services;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Criteria.Aggregates;

/// <summary>
/// The global reservation for one normalized criteria-edition label. Its stream
/// orders competing holders and records the history of this label's own claims.
/// </summary>
/// <remarks>
/// Calls that carry edition event IDs are internal participant decisions. Their
/// caller must read the exact committed edition facts and verify their meaning;
/// an event ID supplied by an untrusted request is not source evidence.
/// </remarks>
public sealed class EditionLabelClaim : Aggregate
{
    /// <summary>The global Fitz realm containing edition-label owners.</summary>
    public const string Realm = "bdgrz";

    /// <summary>The stream area containing edition-label owners.</summary>
    public const string Area = "criteria-edition-labels";

    readonly string _label;
    readonly Uuid _selectedEditionId;
    readonly Uuid _selectedOperationId;
    Reservation? _selected;
    EditionLabelReservationFenced? _selectedFence;
    EditionLabelReservationRefused? _selectedRefusal;
    Reservation? _current;
    long _lastGeneration;

    /// <summary>Opens a label, selecting the exact reservation whose retry history is needed.</summary>
    /// <remarks>
    /// An unselected claim is read-only. Selection bounds retained reservation state;
    /// Portia still loads the whole label stream and no fixed lifetime bound is implied.
    /// </remarks>
    public EditionLabelClaim(string label, Uuid selectedEditionId = default, Uuid selectedOperationId = default)
        : this(Normalize(label), selectedEditionId, selectedOperationId, true)
    {
    }

    EditionLabelClaim(string normalized, Uuid selectedEditionId, Uuid selectedOperationId, bool _)
        : base(IdentityOfNormalized(normalized),
        new EventStreamAddress(Realm, Area, IdentityOfNormalized(normalized).ToString()))
    {
        _label = normalized;
        _selectedEditionId = selectedEditionId;
        _selectedOperationId = selectedOperationId;
        On<EditionLabelReserved>(ApplyReserved);
        On<EditionLabelReservationFenced>(ApplyFence);
        On<EditionLabelReservationRefused>(ApplyRefusal);
        On<EditionLabelConfirmed>(ApplyConfirmed);
        On<EditionLabelReleased>(ApplyReleased);
    }

    /// <summary>The canonical label owned by this stream.</summary>
    public string Label => _label;

    /// <summary>The highest generation recorded by this label, including released claims.</summary>
    public long LastGeneration => _lastGeneration;

    /// <summary>The current edition, or <see cref="Uuid.Empty" /> if the label is free.</summary>
    public Uuid CurrentEditionId => _current?.Reserved.EditionId ?? Uuid.Empty;

    /// <summary>The operation holding the label, or <see cref="Uuid.Empty" />.</summary>
    public Uuid CurrentOperationId => _current?.Reserved.OperationId ?? Uuid.Empty;

    /// <summary>The current holder generation, or zero if the label is free.</summary>
    public long CurrentGeneration => _current?.Reserved.Generation ?? 0;

    /// <summary>Whether the current reservation was confirmed.</summary>
    public bool IsConfirmed => _current?.Confirmed is not null;

    /// <summary>
    /// The current reservation fact, when held. Callers needing authority must
    /// load this fact from committed source rather than trust an uncommitted owner.
    /// </summary>
    public EditionLabelReserved? CurrentReservation => _current?.Reserved;

    /// <summary>The current confirmation fact, if the holder has been confirmed.</summary>
    public EditionLabelConfirmed? CurrentConfirmation => _current?.Confirmed;

    /// <summary>Gets the deterministic owner identity for a valid label.</summary>
    public static Uuid IdentityOf(string label) => IdentityOfNormalized(Normalize(label));

    /// <summary>Gets the deterministic stream address for a valid label.</summary>
    public static EventStreamAddress AddressOf(string label) =>
        new(Realm, Area, IdentityOf(label).ToString());

    /// <summary>Normalizes an edition code without accepting non-ASCII characters.</summary>
    public static bool TryNormalize(string? label, out string normalized)
    {
        normalized = string.Empty;
        if (label is null)
            return false;

        var candidate = label.Trim(' ');
        if (candidate.Length is < 1 or > 128)
            return false;

        var chars = candidate.ToCharArray();
        var previousUnderscore = true;
        for (var index = 0; index < chars.Length; index++)
        {
            var value = chars[index];
            if (value is >= 'A' and <= 'Z')
                value = (char)(value + ('a' - 'A'));

            if (value == '_')
            {
                if (previousUnderscore)
                    return false;
                previousUnderscore = true;
            }
            else if (value is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                previousUnderscore = false;
            }
            else
            {
                return false;
            }

            chars[index] = value;
        }

        if (previousUnderscore)
            return false;
        normalized = new string(chars);
        return true;
    }

    /// <summary>Whether the exact edition operation and generation currently hold this label.</summary>
    public bool IsHeldBy(Uuid editionId, Uuid operationId, long generation) =>
        _current is not null && _current.Reserved.EditionId == editionId &&
        _current.Reserved.OperationId == operationId && _current.Reserved.Generation == generation;

    /// <summary>Returns the last durable status for one edition-scoped operation, when known.</summary>
    public EditionLabelReservationStatus? ReservationStatus(Uuid editionId, Uuid operationId) =>
        Selected(editionId, operationId) ? _selected?.Status : null;

    /// <summary>
    /// Returns the original reservation for an edition operation, including after
    /// release. Callers use its original generation for an exact retry rather
    /// than assigning a new generation to an old operation.
    /// </summary>
    public EditionLabelReserved? ReservationFor(Uuid editionId, Uuid operationId) =>
        Selected(editionId, operationId) ? _selected?.Reserved : null;

    /// <summary>Returns the selected operation's durable refusal, including after the competing holder releases.</summary>
    public EditionLabelReservationRefused? RefusalFor(Uuid editionId, Uuid operationId) =>
        Selected(editionId, operationId) ? _selectedRefusal : null;

    /// <summary>Whether the selected exact edition request has a committed target-side fence.</summary>
    public bool IsRequestFenced(Uuid editionId, Uuid operationId, Uuid requestEventId) =>
        Selected(editionId, operationId) && requestEventId != Uuid.Empty &&
        _selectedFence?.EditionRequestEventId == requestEventId;

    /// <summary>
    /// Reserves the label, or returns the existing operation's status without reacquiring it.
    /// A new operation supplies exactly <see cref="LastGeneration" /> plus one.
    /// </summary>
    public Result<EditionLabelReservationStatus> Reserve(Uuid editionId, Uuid operationId, long generation,
        Uuid editionRequestEventId, ActorAttribution requestedBy)
    {
        if (!ValidIdentity(editionId, operationId, generation) || editionRequestEventId == Uuid.Empty ||
            !ValidActor(requestedBy))
            return Failure<EditionLabelReservationStatus>(RequestErrorKind.Validation,
                "A label reservation requires the edition, operation, generation, source request and actor.");

        if (!Selected(editionId, operationId))
            return Failure<EditionLabelReservationStatus>(RequestErrorKind.Conflict,
                "The exact reservation must be selected before loading the label claim.");

        if (_selected is { } previous)
        {
            var fact = previous.Reserved;
            return fact.Generation == generation && fact.EditionRequestEventId == editionRequestEventId &&
                   fact.RequestedBy == requestedBy
                ? Result<EditionLabelReservationStatus>.Success(previous.Status)
                : Failure<EditionLabelReservationStatus>(RequestErrorKind.Conflict,
                    "The edition operation already has a different reservation intent.");
        }

        if (_selectedFence is not null || _selectedRefusal is not null)
            return Failure<EditionLabelReservationStatus>(RequestErrorKind.Conflict,
                "The exact edition label request was fenced or refused against a delayed reservation.");

        if (_current is not null)
            return Failure<EditionLabelReservationStatus>(RequestErrorKind.Conflict,
                "The edition label is already reserved.");
        if (_lastGeneration == long.MaxValue || generation != _lastGeneration + 1)
            return Failure<EditionLabelReservationStatus>(RequestErrorKind.Conflict,
                "The edition label generation is stale; reload the claim.");

        RaiseEvent(new EditionLabelReserved(_label, editionId, operationId, generation, editionRequestEventId,
            requestedBy));
        return Result<EditionLabelReservationStatus>.Success(EditionLabelReservationStatus.Reserved);
    }

    /// <summary>Records refusal only while a different exact reservation holds the label at this append.</summary>
    /// <remarks>Caller must authenticate the edition request and use qualified source and conditional append.</remarks>
    public Result RefuseContested(Uuid editionId, Uuid operationId, long requestedGeneration,
        Uuid editionRequestEventId, ActorAttribution requestedBy)
    {
        if (!ValidIdentity(editionId, operationId, requestedGeneration) || editionRequestEventId == Uuid.Empty ||
            !ValidActor(requestedBy) || !Selected(editionId, operationId))
            return Failure(RequestErrorKind.Validation, "The selected edition request, generation and actor are required.");
        if (_selectedRefusal is { } previous)
            return previous.RequestedGeneration == requestedGeneration && previous.EditionRequestEventId == editionRequestEventId &&
                   previous.RequestedBy == requestedBy ? Result.Success :
                Failure(RequestErrorKind.Conflict, "The operation already has a different refusal intent.");
        if (_selected is not null || _selectedFence is not null)
            return Failure(RequestErrorKind.Conflict, "The edition request already has a reservation or terminal fence.");
        if (_current is not { } holder || holder.Reserved.EditionId == editionId && holder.Reserved.OperationId == operationId ||
            holder.Reserved.Metadata.EventId == Uuid.Empty || _lastGeneration == long.MaxValue ||
            requestedGeneration != _lastGeneration + 1)
            return Failure(RequestErrorKind.Conflict, "A different exact live holder and the next generation are required.");
        RaiseEvent(new EditionLabelReservationRefused(_label, editionId, operationId, requestedGeneration,
            editionRequestEventId, requestedBy, holder.Reserved.EditionId, holder.Reserved.OperationId,
            holder.Reserved.Generation, holder.Reserved.Metadata.EventId));
        return Result.Success;
    }

    /// <summary>Confirms an exact held reservation against its committed edition acceptance.</summary>
    public Result Confirm(Uuid editionId, Uuid operationId, long generation, Uuid editionAcceptedEventId)
    {
        if (!ValidIdentity(editionId, operationId, generation) || editionAcceptedEventId == Uuid.Empty)
            return Failure(RequestErrorKind.Validation,
                "A confirmation requires the exact reservation and committed edition acceptance.");
        if (!Selected(editionId, operationId) || _selected is not { } reservation ||
            reservation.Reserved.Generation != generation)
            return Failure(RequestErrorKind.Conflict, "The exact edition label reservation was not found.");

        if (reservation.Confirmed is { } previous)
            return previous.EditionAcceptedEventId == editionAcceptedEventId
                ? Result.Success
                : Failure(RequestErrorKind.Conflict, "The reservation was confirmed against another edition fact.");
        if (!IsHeldBy(editionId, operationId, generation) || reservation.Released is not null)
            return Failure(RequestErrorKind.Conflict, "The edition label reservation is no longer held.");

        RaiseEvent(new EditionLabelConfirmed(_label, editionId, operationId, generation, editionAcceptedEventId));
        return Result.Success;
    }

    /// <summary>Releases an exact held claim after its committed edition transition is verified.</summary>
    public Result Release(Uuid editionId, Uuid operationId, long generation, Uuid editionTerminalEventId,
        string reason, Uuid replacementConfirmationEventId = default)
    {
        if (!ValidIdentity(editionId, operationId, generation) || editionTerminalEventId == Uuid.Empty ||
            !ValidRelease(reason, replacementConfirmationEventId))
            return Failure(RequestErrorKind.Validation,
                "A release requires the exact reservation, committed edition transition and applicable evidence.");
        if (!Selected(editionId, operationId) || _selected is not { } reservation ||
            reservation.Reserved.Generation != generation)
            return Failure(RequestErrorKind.Conflict, "The exact edition label reservation was not found.");

        if (reservation.Released is { } previous)
            return previous.EditionTerminalEventId == editionTerminalEventId && previous.Reason == reason &&
                   previous.ReplacementConfirmationEventId == replacementConfirmationEventId
                ? Result.Success
                : Failure(RequestErrorKind.Conflict, "The reservation was released under a different transition.");
        if (!IsHeldBy(editionId, operationId, generation))
            return Failure(RequestErrorKind.Conflict, "The edition label reservation is no longer held.");
        if (reason == "renamed" && reservation.Confirmed is null)
            return Failure(RequestErrorKind.Conflict, "An unconfirmed label cannot be released as a completed rename.");

        RaiseEvent(new EditionLabelReleased(_label, editionId, operationId, generation, editionTerminalEventId,
            reason, replacementConfirmationEventId));
        return Result.Success;
    }

    /// <summary>Fences a terminal request and releases its exact held claim in one append if necessary.</summary>
    /// <remarks>The caller must source-verify the request and terminal decision. A different holder is untouched.</remarks>
    public Result FenceTerminalRequest(Uuid editionId, Uuid operationId, Uuid editionRequestEventId,
        Uuid editionTerminalEventId, string reason)
    {
        if (!Selected(editionId, operationId) || editionRequestEventId == Uuid.Empty ||
            editionTerminalEventId == Uuid.Empty || reason is not ("cancelled" or "refused"))
            return Failure(RequestErrorKind.Validation, "The selected terminal label request and its exact source facts are required.");
        if (_selectedRefusal is { } refusal && refusal.EditionRequestEventId != editionRequestEventId)
            return Failure(RequestErrorKind.Conflict, "The refusal names a different edition request.");
        if (_selectedFence is { } previous)
        {
            if (previous.EditionRequestEventId != editionRequestEventId ||
                previous.EditionTerminalEventId != editionTerminalEventId || previous.Reason != reason)
                return Failure(RequestErrorKind.Conflict, "The request was fenced under different source facts.");
            return _selected is { Released: null } currentlyHeld
                ? Release(editionId, operationId, currentlyHeld.Reserved.Generation, editionTerminalEventId, reason)
                : Result.Success;
        }
        if (_selected is { } reservation)
        {
            if (reservation.Reserved.EditionRequestEventId != editionRequestEventId)
                return Failure(RequestErrorKind.Conflict, "The reservation names a different edition request.");
            if (reservation.Released is { } released)
                return released.EditionTerminalEventId == editionTerminalEventId && released.Reason == reason
                    ? Result.Success : Failure(RequestErrorKind.Conflict, "The reservation was released under a different transition.");
            if (!IsHeldBy(editionId, operationId, reservation.Reserved.Generation))
                return Failure(RequestErrorKind.Conflict, "The exact selected reservation is not the current holder.");
        }

        RaiseEvent(new EditionLabelReservationFenced(_label, editionId, operationId, editionRequestEventId,
            editionTerminalEventId, reason));
        if (_selected is { } held)
            return Release(editionId, operationId, held.Reserved.Generation, editionTerminalEventId, reason);
        return Result.Success;
    }

    void ApplyReserved(EditionLabelReserved fact)
    {
        if (fact.Label != _label || !ValidIdentity(fact.EditionId, fact.OperationId, fact.Generation) ||
            fact.EditionRequestEventId == Uuid.Empty || !ValidActor(fact.RequestedBy) || _current is not null ||
            _lastGeneration == long.MaxValue || fact.Generation != _lastGeneration + 1 ||
            (Selected(fact.EditionId, fact.OperationId) && (_selected is not null || _selectedFence is not null || _selectedRefusal is not null)))
            throw new InvalidOperationException("A label reservation must claim the free label at its next generation.");

        var reservation = new Reservation(fact);
        if (Selected(fact.EditionId, fact.OperationId))
            _selected = reservation;
        _current = reservation;
        _lastGeneration = fact.Generation;
    }

    void ApplyFence(EditionLabelReservationFenced fact)
    {
        if (fact.Label != _label || fact.EditionId == Uuid.Empty || fact.OperationId == Uuid.Empty ||
            fact.EditionRequestEventId == Uuid.Empty || fact.EditionTerminalEventId == Uuid.Empty ||
            fact.Reason is not ("cancelled" or "refused") ||
            Selected(fact.EditionId, fact.OperationId) &&
            (_selectedFence is not null || _selectedRefusal is { } refusal && refusal.EditionRequestEventId != fact.EditionRequestEventId ||
            _selected is { } reservation &&
                reservation.Reserved.EditionRequestEventId != fact.EditionRequestEventId))
            throw new InvalidOperationException("A label fence must name its exact terminal request once.");
        if (Selected(fact.EditionId, fact.OperationId))
            _selectedFence = fact;
    }

    void ApplyRefusal(EditionLabelReservationRefused fact)
    {
        if (fact.Label != _label || !ValidIdentity(fact.EditionId, fact.OperationId, fact.RequestedGeneration) ||
            fact.EditionRequestEventId == Uuid.Empty || !ValidActor(fact.RequestedBy) ||
            _current is not { } holder || holder.Reserved.EditionId == fact.EditionId && holder.Reserved.OperationId == fact.OperationId ||
            holder.Reserved.EditionId != fact.HolderEditionId || holder.Reserved.OperationId != fact.HolderOperationId ||
            holder.Reserved.Generation != fact.HolderGeneration || holder.Reserved.Metadata.EventId != fact.HolderReservationEventId ||
            fact.HolderReservationEventId == Uuid.Empty || _lastGeneration == long.MaxValue ||
            fact.RequestedGeneration != _lastGeneration + 1 ||
            Selected(fact.EditionId, fact.OperationId) && (_selected is not null || _selectedFence is not null || _selectedRefusal is not null))
            throw new InvalidOperationException("A label refusal must name the exact different live holder and selected request.");
        if (Selected(fact.EditionId, fact.OperationId))
            _selectedRefusal = fact;
    }

    void ApplyConfirmed(EditionLabelConfirmed fact)
    {
        if (fact.Label != _label || fact.EditionAcceptedEventId == Uuid.Empty ||
            !IsHeldBy(fact.EditionId, fact.OperationId, fact.Generation) || _current?.Confirmed is not null)
            throw new InvalidOperationException("A label confirmation must name the exact held reservation once.");
        _current!.Confirmed = fact;
    }

    void ApplyReleased(EditionLabelReleased fact)
    {
        if (fact.Label != _label || fact.EditionTerminalEventId == Uuid.Empty ||
            !ValidRelease(fact.Reason, fact.ReplacementConfirmationEventId) ||
            !IsHeldBy(fact.EditionId, fact.OperationId, fact.Generation) ||
            fact.Reason == "renamed" && _current?.Confirmed is null)
            throw new InvalidOperationException("A label release must name its exact held reservation and transition.");
        _current!.Released = fact;
        _current = null;
    }

    static Uuid IdentityOfNormalized(string normalized) => AggregateIdentity.Create(nameof(EditionLabelClaim), normalized);

    bool Selected(Uuid editionId, Uuid operationId) => editionId != Uuid.Empty && operationId != Uuid.Empty &&
        editionId == _selectedEditionId && operationId == _selectedOperationId;

    static string Normalize(string? label) => TryNormalize(label, out var normalized)
        ? normalized
        : throw new ArgumentException("A valid structured edition label is required.", nameof(label));

    static bool ValidIdentity(Uuid editionId, Uuid operationId, long generation) =>
        editionId != Uuid.Empty && operationId != Uuid.Empty && generation > 0;

    static bool ValidActor(ActorAttribution? actor) => actor is not null &&
        !string.IsNullOrWhiteSpace(actor.Subject) && !string.IsNullOrWhiteSpace(actor.Issuer);

    static bool ValidRelease(string? reason, Uuid replacementConfirmationEventId) => reason switch
    {
        "renamed" => replacementConfirmationEventId != Uuid.Empty,
        "abandoned" or "cancelled" or "refused" => replacementConfirmationEventId == Uuid.Empty,
        _ => false,
    };

    static Result Failure(RequestErrorKind kind, string message) => Result.Failure(new RequestError(kind, message));

    static Result<T> Failure<T>(RequestErrorKind kind, string message) =>
        Result<T>.Failure(new RequestError(kind, message));

    sealed class Reservation(EditionLabelReserved reserved)
    {
        public EditionLabelReserved Reserved { get; } = reserved;
        public EditionLabelConfirmed? Confirmed { get; set; }
        public EditionLabelReleased? Released { get; set; }
        public EditionLabelReservationStatus Status => Released is not null ? EditionLabelReservationStatus.Released :
            Confirmed is not null ? EditionLabelReservationStatus.Confirmed : EditionLabelReservationStatus.Reserved;
    }
}
