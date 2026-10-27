using Bdgrz.Compliance.Features.Kernel;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.SourceUse;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessControl;

/// <summary>Permanent practice authority, live reservations and one selected historical receipt for one subject/client.</summary>
public sealed class PracticeIndependenceCommitment : Aggregate, ISourceUseOwner
{
    public const string Area = "client-practice-commitments";
    public const string Advisory = "advisory";
    public const string Attest = "attest";

    readonly Dictionary<SourceUseSlot, PracticeIndependenceCommitted> _liveCommitted = [];
    PracticeIndependenceCommitted? _first;
    PracticeIndependenceCommitted? _latest;
    PracticeIndependenceCommitted? _selected;
    ulong _revision;

    public PracticeIndependenceCommitment(Uuid tenantId, PracticeSubjectKind kind, Uuid subjectId,
        Uuid selectedLogicalId = default, string? practice = null, Uuid engagementId = default,
        ReservationKey? selectedReservation = null)
        : base(AggregateIdentity.Create(nameof(PracticeIndependenceCommitment), tenantId, PracticeSubjects.Wire(kind), subjectId),
            new EventStreamAddress(tenantId.ToString(), Area, PracticeSubjects.Resource(kind, subjectId)))
    {
        TenantId = tenantId;
        Kind = kind;
        SubjectId = subjectId;
        IntendedPractice = practice;
        IntendedEngagementId = engagementId;
        SelectedReservation = selectedReservation;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        Uses = new SourceUseReservations(Stream);
        On<PracticeIndependenceReserved>(reserved =>
        {
            Acceptance.Observe(reserved);
            if (reserved.Owner != Stream || reserved.Operation.Owner != Stream || reserved.Operation.RootOperationId == Uuid.Empty ||
                reserved.EngagementId == Uuid.Empty || reserved.Practice is not (Advisory or Attest) ||
                reserved.Revision != (long)_revision || Practice is not null && Practice != reserved.Practice ||
                ActiveReservations().Any(other => other.Practice != reserved.Practice))
                throw new InvalidOperationException("A subject reserves one practice at a time and never contradicts its permanent wall.");
            Uses.ApplyGranted(SourceUsePurpose.ActualStaffAssignment, reserved);
        });
        On<PracticeIndependenceCommitted>(committed =>
        {
            Acceptance.Observe(committed);
            var slot = Slot(committed.Operation, committed.Generation);
            var reservation = ActiveReservations().SingleOrDefault(value => Slot(value.Operation, value.Generation) == slot);
            if (committed.Owner != Stream || committed.Operation.Owner != Stream || reservation is null ||
                reservation.Practice != committed.Practice ||
                reservation.EngagementId != committed.EngagementId || Practice is not null && Practice != committed.Practice ||
                committed.Revision != (long)_revision + 1 || !_liveCommitted.TryAdd(slot, committed))
                throw new InvalidOperationException("A commitment follows its exact active reservation once and never contradicts the permanent practice.");
            _first ??= committed;
            _latest = committed;
            _revision++;
            if (SelectedReservation is { } selected && SourceUseSlot.Of(Stream, selected) == slot)
                _selected = committed;
        });
        On<PracticeIndependenceReservationSettled>(settled =>
        {
            Acceptance.Observe(settled);
            Uses.ApplySettled(SourceUsePurpose.ActualStaffAssignment, settled);
            _liveCommitted.Remove(Slot(settled.Operation, settled.Generation));
        });
    }

    public SourceUseReservations Uses { get; }
    public Uuid TenantId { get; }
    public PracticeSubjectKind Kind { get; }
    public Uuid SubjectId { get; }
    public NativeAcceptance Acceptance { get; }
    internal string? IntendedPractice { get; }
    internal Uuid IntendedEngagementId { get; }
    public ReservationKey? SelectedReservation { get; }
    public string? Practice => _first?.Practice;
    public bool IsCommitted => _first is not null;
    public ulong Revision => _revision;
    public PracticeIndependenceCommitted? FirstCommitment => _first;
    internal PracticeIndependenceCommitted? LatestCommitment => _latest;
    internal IReadOnlyDictionary<SourceUseSlot, PracticeIndependenceCommitted> LiveCommitted => _liveCommitted;
    internal FirmIndependenceReplayCut ReplayCut { get; } = new();

    /// <summary>A live or explicitly selected commitment, including its first-owner/generation identity.</summary>
    public PracticeIndependenceCommitted? CommitmentOf(ReservationKey key)
    {
        var slot = SourceUseSlot.Of(Stream, key);
        if (_liveCommitted.TryGetValue(slot, out var live))
            return live;
        if (SelectedReservation == key)
            return _selected;
        if (Version == 0)
            return null;
        throw new InvalidOperationException("Historical practice commitment lookup requires explicit exact reservation selection.");
    }

    public bool Serves(string practice) => Practice == practice;

    public Result PrepareReservation(Uuid rootOperationId, string practice)
    {
        if (practice is not (Advisory or Attest))
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "The practice is invalid."));
        if (Practice is not null && Practice != practice)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The subject permanently serves the opposite practice for this client."));
        return ActiveReservations().Any(other => other.Practice != practice)
            ? Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Another operation holds the opposite practice for this subject; retry after it settles.", true))
            : Result.Success;
    }

    public Result Reserve(OperationProvenance operation, ImmutableContentRef intent, long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream || IntendedPractice is null || IntendedEngagementId == Uuid.Empty)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A reservation is recorded on its subject for a declared practice and engagement."));
        var ready = PrepareReservation(operation.RootOperationId, IntendedPractice);
        if (!ready.IsSuccess)
            return ready;
        Uses.RequireForAppend(SourceUsePurpose.ActualStaffAssignment, operation, generation, Version, CommittedStreamPosition);
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        RaiseEvent(new PracticeIndependenceReserved(Stream, (long)_revision, generation, intent, null, operation,
            null, null, IntendedPractice, IntendedEngagementId));
        return Result.Success;
    }

    public Result Commit(OperationProvenance operation, ImmutableContentRef intent, long generation, string practice,
        Uuid engagementId)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "A commitment is recorded on its subject stream."));
        var slot = Slot(operation, generation);
        var reservation = ActiveReservations().SingleOrDefault(value => Slot(value.Operation, value.Generation) == slot);
        if (reservation is null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The exact active reservation of this operation is not held.", true));
        if (reservation.Practice != practice || reservation.EngagementId != engagementId ||
            Practice is not null && Practice != practice || _liveCommitted.ContainsKey(slot))
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The commitment differs from its exact reservation or is already permanent."));
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        RaiseEvent(new PracticeIndependenceCommitted(Stream, (long)_revision + 1, generation, intent, null,
            operation, null, null, practice, engagementId));
        return Result.Success;
    }

    public Result SettleReservation(OperationProvenance operation, ImmutableContentRef intent, long generation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Validation, "A settlement is recorded on its subject stream."));
        Uses.RequireForAppend(SourceUsePurpose.ActualStaffAssignment, operation, generation, Version, CommittedStreamPosition);
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        RaiseEvent(new PracticeIndependenceReservationSettled(Stream, (long)_revision, generation, intent, null,
            operation, null, null));
        return Result.Success;
    }

    IEnumerable<PracticeIndependenceReserved> ActiveReservations() => Uses.ActiveUses.Select(entry => entry.GrantFact)
        .OfType<PracticeIndependenceReserved>();
    SourceUseSlot Slot(OperationProvenance operation, long generation) =>
        SourceUseSlot.Of(Stream, SourceUsePurpose.ActualStaffAssignment, operation, generation);
    protected override void Apply(DomainEvent ev)
    {
        if (!ev.Metadata.IsAudit)
            ReplayCut.Observe(ev);
        base.Apply(ev);
        Uses.ObserveOwnerFact(ev);
    }
}
