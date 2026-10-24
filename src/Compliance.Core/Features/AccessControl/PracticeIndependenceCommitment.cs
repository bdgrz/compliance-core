using System.Globalization;
using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>
///     The permanent practice binding of one subject, a staff member or a canonical user, for one client. An
///     acceptance or staff assignment first reserves the subject temporarily and exclusively for its practice, and only
///     after the engagement's irreversible authorization commits the reservation permanently. A commitment is never
///     released, revoked or reactivated: the owner has no command that removes it, and closing the engagement, removing
///     the assignment, deactivating the staff record or settling the reservation leave it in place. A subject that
///     committed to one practice can never reserve or commit the opposite practice for this client.
/// </summary>
public sealed class PracticeIndependenceCommitment : Aggregate, ISourceUseOwner
{
    /// <summary>Stream area of every practice subject. It sorts before the client service owner of the same tenant.</summary>
    public const string Area = "client-practice-commitments";

    /// <summary>The practice of an advisory engagement.</summary>
    public const string Advisory = "advisory";

    /// <summary>The practice of an attest engagement.</summary>
    public const string Attest = "attest";

    readonly Dictionary<Uuid, Reservation> _reservations = [];
    readonly List<PracticeIndependenceCommitted> _commitments = [];
    string? _practice;

    /// <summary>
    ///     Opens the subject of one client, selecting the logical operation to resolve. <paramref name="practice" /> and
    ///     <paramref name="engagementId" /> are the terms of the reservation the caller is about to raise; they are not
    ///     state.
    /// </summary>
    public PracticeIndependenceCommitment(Uuid tenantId, PracticeSubjectKind kind, Uuid subjectId,
        Uuid selectedLogicalId = default, string? practice = null, Uuid engagementId = default)
        : base(AggregateIdentity.Create(nameof(PracticeIndependenceCommitment), tenantId, PracticeSubjects.Wire(kind),
                subjectId),
            new EventStreamAddress(tenantId.ToString(), Area, PracticeSubjects.Resource(kind, subjectId)))
    {
        TenantId = tenantId;
        Kind = kind;
        SubjectId = subjectId;
        IntendedPractice = practice;
        IntendedEngagementId = engagementId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        Uses = new SourceUseReservations(Stream);
        On<PracticeIndependenceReserved>(reserved =>
        {
            Acceptance.Observe(reserved);
            if (reserved.Owner != Stream || reserved.Operation.Owner != Stream ||
                reserved.Operation.RootOperationId == Uuid.Empty || reserved.EngagementId == Uuid.Empty ||
                reserved.Practice is not (Advisory or Attest) || reserved.Revision != _commitments.Count ||
                _practice is not null && _practice != reserved.Practice ||
                _reservations.Values.Any(other => other.IsActive && other.Practice != reserved.Practice))
                throw new InvalidOperationException(
                    "A subject is reserved for one practice at a time and never against its permanent practice.");
            Uses.ApplyGranted(SourceUsePurpose.ActualStaffAssignment, reserved);
            _reservations.Add(reserved.Operation.RootOperationId, new Reservation(reserved));
        });
        On<PracticeIndependenceCommitted>(committed =>
        {
            Acceptance.Observe(committed);
            if (committed.Owner != Stream || committed.Operation.Owner != Stream ||
                !_reservations.TryGetValue(committed.Operation.RootOperationId, out var reservation) ||
                !reservation.IsActive || reservation.Committed is not null ||
                reservation.Reserved.Practice != committed.Practice ||
                reservation.Reserved.EngagementId != committed.EngagementId ||
                _practice is not null && _practice != committed.Practice ||
                committed.Revision != _commitments.Count + 1)
                throw new InvalidOperationException(
                    "A permanent commitment follows its own active reservation and never contradicts the permanent practice.");
            reservation.Committed = committed;
            _commitments.Add(committed);
            _practice = committed.Practice;
        });
        On<PracticeIndependenceReservationSettled>(settled =>
        {
            Acceptance.Observe(settled);
            Uses.ApplySettled(SourceUsePurpose.ActualStaffAssignment, settled);
            if (_reservations.TryGetValue(settled.Operation.RootOperationId, out var reservation))
                reservation.Settled = settled;
        });
    }

    /// <summary>The active reservations, source use generation and fences of this subject.</summary>
    public SourceUseReservations Uses { get; }

    /// <summary>The client tenant of the subject.</summary>
    public Uuid TenantId { get; }

    /// <summary>Whether the subject is a staff member or a canonical user.</summary>
    public PracticeSubjectKind Kind { get; }

    /// <summary>The staff member or canonical user this owner binds.</summary>
    public Uuid SubjectId { get; }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The practice a reservation about to be raised is for, when the caller declared one.</summary>
    internal string? IntendedPractice { get; }

    /// <summary>The engagement a reservation about to be raised is for, when the caller declared one.</summary>
    internal Uuid IntendedEngagementId { get; }

    /// <summary>The permanent practice, once the first commitment was made; never changes afterwards.</summary>
    public string? Practice => _practice;

    /// <summary>True once any permanent commitment exists.</summary>
    public bool IsCommitted => _commitments.Count > 0;

    /// <summary>The logical revision: the number of permanent commitments.</summary>
    public ulong Revision => (ulong)_commitments.Count;

    /// <summary>Every permanent commitment, in order.</summary>
    public IReadOnlyList<PracticeIndependenceCommitted> Commitments => _commitments;

    /// <summary>The permanent commitment an operation made, when it made one.</summary>
    public PracticeIndependenceCommitted? CommitmentOf(Uuid rootOperationId) =>
        _reservations.GetValueOrDefault(rootOperationId)?.Committed;

    /// <summary>The temporary reservation an operation holds or held, when it reserved this subject.</summary>
    public PracticeIndependenceReserved? ReservationOf(Uuid rootOperationId) =>
        _reservations.GetValueOrDefault(rootOperationId)?.Reserved;

    /// <summary>True when the subject permanently serves exactly this practice for the client.</summary>
    public bool Serves(string practice) => _practice == practice;

    /// <summary>The permanent assignments of this subject as the client independence rules consume them.</summary>
    public IReadOnlyList<EngagementAssignment> Assignments()
    {
        var assignments = new List<EngagementAssignment>(_commitments.Count);
        foreach (var committed in _commitments)
        {
            var practice = ServiceEngagementRules.PracticeOf(committed.Practice);
            assignments.Add(Kind == PracticeSubjectKind.Staff
                ? new EngagementAssignment(TenantId, committed.EngagementId, SubjectId, Uuid.Empty, practice)
                : new EngagementAssignment(TenantId, committed.EngagementId, Uuid.Empty, SubjectId, practice));
        }

        return assignments;
    }

    /// <summary>
    ///     Checks whether this subject can be reserved or committed for a practice: never against a permanent
    ///     practice (a permanent conflict), and never while another operation holds the opposite practice (transient,
    ///     because that operation may still be rejected).
    /// </summary>
    public Result PrepareReservation(Uuid rootOperationId, string practice)
    {
        if (practice is not (Advisory or Attest))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "The practice is invalid."));
        if (_practice is not null && _practice != practice)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The subject permanently serves the opposite practice for this client.", false));
        return _reservations.Any(pair => pair.Key != rootOperationId && pair.Value.IsActive &&
            pair.Value.Practice != practice)
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Another operation holds the opposite practice for this subject; retry after it settles.", true))
            : Result.Success;
    }

    /// <summary>Reserves the subject temporarily for the practice and engagement this instance was opened for.</summary>
    public Result Reserve(OperationProvenance operation, ImmutableContentRef intent, long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream || IntendedPractice is null || IntendedEngagementId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A reservation is recorded on its own subject stream for a declared practice and engagement."));
        var prepared = PrepareReservation(operation.RootOperationId, IntendedPractice);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new PracticeIndependenceReserved(Stream, _commitments.Count, generation, intent, null, operation,
            null, null, IntendedPractice, IntendedEngagementId));
        return Result.Success;
    }

    /// <summary>Makes the permanent commitment of the exact active reservation of the operation.</summary>
    public Result Commit(OperationProvenance operation, ImmutableContentRef intent, long generation, string practice,
        Uuid engagementId)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A commitment is recorded on its own subject stream."));
        if (!_reservations.TryGetValue(operation.RootOperationId, out var reservation) || !reservation.IsActive)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The exact active reservation of this operation is not held; no commitment is made without it.", true));
        if (reservation.Reserved.Practice != practice || reservation.Reserved.EngagementId != engagementId ||
            _practice is not null && _practice != practice)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The commitment differs from the reserved practice or engagement.", false));
        RaiseEvent(new PracticeIndependenceCommitted(Stream, _commitments.Count + 1, generation, intent, null,
            operation, null, null, practice, engagementId));
        return Result.Success;
    }

    /// <summary>Settles the temporary reservation of the operation, or fences its key when none exists.</summary>
    public Result SettleReservation(OperationProvenance operation, ImmutableContentRef intent, long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A settlement is recorded on the reserved subject stream."));
        RaiseEvent(new PracticeIndependenceReservationSettled(Stream, _commitments.Count, generation, intent, null,
            operation, null, null));
        return Result.Success;
    }

    sealed class Reservation(PracticeIndependenceReserved reserved)
    {
        public PracticeIndependenceReserved Reserved { get; } = reserved;

        public string Practice => Reserved.Practice;

        public PracticeIndependenceCommitted? Committed { get; set; }

        public PracticeIndependenceReservationSettled? Settled { get; set; }

        public bool IsActive => Settled is null;
    }
}
