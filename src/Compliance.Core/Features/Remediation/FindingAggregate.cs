using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Remediation;

/// <summary>
///     One finding: its raised terms, triage, acceptance links, the admitted corrective actions of its generations
///     and the closure decision. The logical revision counts every fact of this finding stream, and the generation
///     starts at one and advances on every reopen. A corrective action is admitted here and materialized as its own
///     owner; a closure seals admission for its generation and only closes once the closure process appends
///     <see cref="FindingClosedV2" />. Reducers never read content or another owner.
/// </summary>
public sealed class FindingAggregate : Aggregate
{
    /// <summary>Stream area of every finding (the realm is the tenant).</summary>
    public const string Area = "findings";

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    readonly Uuid _findingId;
    readonly List<CorrectiveActionAdmitted> _admitted = [];
    readonly HashSet<Uuid> _admittedIds = [];
    readonly List<FindingAcceptanceLinkedV2> _acceptances = [];
    readonly List<FindingClosedV2> _closures = [];
    readonly Dictionary<Uuid, FindingClosureAccepted> _accepted = [];
    readonly Dictionary<Uuid, DomainEvent> _terminal = [];
    readonly List<DomainEvent> _facts = [];
    FindingRaisedV2? _raised;
    FindingRevisedV2? _revised;
    FindingClosureAccepted? _pending;
    FindingClosedV2? _closed;
    long _revision;
    long _generation;

    /// <summary>Opens one finding, selecting the logical operation to resolve.</summary>
    public FindingAggregate(Uuid tenantId, Uuid programId, Uuid findingId, Uuid selectedLogicalId = default)
        : base(StreamId(programId, findingId), new EventStreamAddress(tenantId.ToString(), Area,
            StreamId(programId, findingId).ToString()))
    {
        if (tenantId == Uuid.Empty || programId == Uuid.Empty || findingId == Uuid.Empty)
            throw new ArgumentException("A finding stream needs tenant, program, and finding IDs.");
        (_tenantId, _programId, _findingId) = (tenantId, programId, findingId);
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<FindingRaisedV2>(Apply);
        On<FindingRevisedV2>(Apply);
        On<CorrectiveActionAdmitted>(Apply);
        On<FindingAcceptanceLinkedV2>(Apply);
        On<FindingClosureAccepted>(Apply);
        On<FindingClosedV2>(Apply);
        On<FindingClosureCanceled>(Apply);
        On<FindingReopenedV2>(Apply);
    }

    /// <summary>The stream identity of a finding.</summary>
    public static Uuid StreamId(Uuid programId, Uuid findingId)
    {
        if (programId == Uuid.Empty || findingId == Uuid.Empty)
            throw new ArgumentException("A finding stream needs program and finding IDs.");
        return Uuid.CreateVersion5(programId, $"finding:{findingId}");
    }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The program the finding belongs to.</summary>
    public Uuid ProgramId => _programId;

    /// <summary>The finding identity.</summary>
    public Uuid FindingId => _findingId;

    /// <summary>True once the finding was raised.</summary>
    public bool IsRecorded => _raised is not null;

    /// <summary>The raise fact, once recorded.</summary>
    public FindingRaisedV2? Raised => _raised;

    /// <summary>The logical revision: the number of facts of this finding.</summary>
    public long Revision => _revision;

    /// <summary>The current generation: one at the raise, plus one for every reopen.</summary>
    public long Generation => _generation;

    /// <summary>The current severity.</summary>
    public string? Severity => _revised?.Severity ?? _raised?.Severity;

    /// <summary>The current owner.</summary>
    public Uuid OwnerMemberId => _revised?.OwnerMemberId ?? _raised?.OwnerMemberId ?? Uuid.Empty;

    /// <summary>The current due date.</summary>
    public DateOnly DueOn => _revised?.DueOn ?? _raised?.DueOn ?? default;

    /// <summary>The current affected scope.</summary>
    public string? AffectedScope => _revised?.AffectedScope ?? _raised?.AffectedScope;

    /// <summary>The current root cause.</summary>
    public string? RootCause => _revised?.RootCause;

    /// <summary>The corrective actions admitted so far, in admission order, across every generation.</summary>
    public IReadOnlyList<CorrectiveActionAdmitted> Admitted => _admitted;

    /// <summary>The acceptance links in order.</summary>
    public IReadOnlyList<FindingAcceptanceLinkedV2> Acceptances => _acceptances;

    /// <summary>The closure accepted and not yet closed or canceled, which seals action admission.</summary>
    public FindingClosureAccepted? PendingClosure => _pending;

    /// <summary>The closure fact of the current generation; null while open or after a reopen.</summary>
    public FindingClosedV2? Closed => _closed;

    /// <summary>Every closure the finding ever recorded, across reopened generations.</summary>
    public IReadOnlyList<FindingClosedV2> ClosureHistory => _closures;

    /// <summary>Every fact in order.</summary>
    public IReadOnlyList<DomainEvent> Facts => _facts;

    /// <summary>True when an action with this id was admitted.</summary>
    public bool HasAdmitted(Uuid actionId) => _admittedIds.Contains(actionId);

    /// <summary>The closure accepted for a root operation, or null.</summary>
    public FindingClosureAccepted? AcceptedClosure(Uuid rootOperationId) => _accepted.GetValueOrDefault(rootOperationId);

    /// <summary>The closed or canceled fact that ended a root operation, or null while it is pending.</summary>
    public DomainEvent? TerminalClosure(Uuid rootOperationId) => _terminal.GetValueOrDefault(rootOperationId);

    /// <summary>The fact with this event id.</summary>
    public DomainEvent? FactById(Uuid eventId) => _facts.FirstOrDefault(fact => fact.Metadata?.EventId == eventId);

    /// <summary>Checks that the finding can take an ordinary write at the expected revision.</summary>
    public Result PrepareWrite(long expectedRevision)
    {
        if (!IsRecorded)
            return Fail(CommandFailure.MissingRecord("The finding was not found."));
        if (expectedRevision != _revision)
            return Fail(CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("finding", _revision)));
        if (_pending is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "A closure of this finding is in progress; retry when it settles.", true));
        return _closed is not null
            ? Fail(CommandFailure.StateConflict("The finding is closed. Reopen it to change it."))
            : Result.Success;
    }

    /// <summary>Checks that a closure can be accepted: the write guards and at least one admitted action.</summary>
    public Result PrepareClosure(long expectedRevision)
    {
        var prepared = PrepareWrite(expectedRevision);
        if (!prepared.IsSuccess)
            return prepared;
        return _admitted.Count == 0
            ? Fail(CommandFailure.StateConflict(
                "Closing a finding requires completed corrective work; an acceptance is not remediation."))
            : Result.Success;
    }

    /// <summary>Checks that a closed finding can be reopened at the expected revision.</summary>
    public Result PrepareReopen(long expectedRevision)
    {
        if (!IsRecorded)
            return Fail(CommandFailure.MissingRecord("The finding was not found."));
        if (expectedRevision != _revision)
            return Fail(CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("finding", _revision)));
        return _closed is null
            ? Fail(CommandFailure.StateConflict("Only a closed finding can be reopened."))
            : Result.Success;
    }

    /// <summary>Checks that the finding does not exist yet.</summary>
    public Result PrepareRaise() => IsRecorded
        ? Fail(CommandFailure.StateConflict("This finding identity already has a different raised record."))
        : Result.Success;

    /// <summary>Raises the finding as the root native fact of its operation, with its initial action when routed.</summary>
    public Result Raise(OperationProvenance operation, FindingRaiseTerms terms, ImmutableContentRef links,
        ImmutableContentRef? firstResponse, ImmutableContentRef? operationResult, InitialActionAdmission? initial)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(terms);
        ArgumentNullException.ThrowIfNull(links);
        if (operation.Owner != Stream || operation.LogicalId != _findingId)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A raise is the logical operation of its own finding."));
        var prepared = PrepareRaise();
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new FindingRaisedV2(operation.LogicalId, _tenantId, _programId, _findingId, 1, terms.Source,
            terms.Title, terms.Description, terms.Severity, terms.AffectedScope, terms.OwnerMemberId, terms.DueOn,
            links, terms.RaisedBy, terms.RaisedAt, terms.CreationKind, terms.InitialActionSha256, operation,
            firstResponse, operationResult));
        if (initial is not null)
            RaiseEvent(new CorrectiveActionAdmitted(Stream, 2, 1, initial.Intent, null, initial.Operation, null, null));
        return Result.Success;
    }

    /// <summary>Records a triage revision as the root native fact of its operation.</summary>
    public Result Revise(OperationProvenance operation, FindingRevision revision, ImmutableContentRef firstResponse,
        ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(revision);
        var prepared = PrepareOwnWrite(operation);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new FindingRevisedV2(operation.LogicalId, _tenantId, _programId, _findingId, _revision + 1,
            revision.Severity, revision.OwnerMemberId, revision.DueOn, revision.AffectedScope, revision.RootCause,
            revision.Reason, revision.RevisedBy, revision.RevisedAt, operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Links an acceptance as the root native fact of its operation.</summary>
    public Result LinkAcceptance(OperationProvenance operation, ImmutableContentRef acceptance,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(acceptance);
        var prepared = PrepareOwnWrite(operation);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new FindingAcceptanceLinkedV2(operation.LogicalId, _tenantId, _programId, _findingId,
            _revision + 1, acceptance, operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Admits one corrective action for the current generation as the root native fact of its operation.</summary>
    public Result AdmitAction(OperationProvenance operation, ImmutableContentRef intent, ImmutableContentRef firstResponse)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        var prepared = PrepareOwnWrite(operation);
        if (!prepared.IsSuccess)
            return prepared;
        if (_admittedIds.Contains(operation.LogicalId))
            return Fail(CommandFailure.StateConflict("A corrective action identity cannot be reused."));
        RaiseEvent(new CorrectiveActionAdmitted(Stream, _revision + 1, _generation, intent, null, operation,
            firstResponse, null));
        return Result.Success;
    }

    /// <summary>Accepts a closure as the root native fact of its operation; it seals admission for the generation.</summary>
    public Result AcceptClosure(OperationProvenance operation, ImmutableContentRef intent, ImmutableContentRef firstResponse)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A closure is a root operation of its own finding with an expected revision."));
        var prepared = PrepareClosure((long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new FindingClosureAccepted(Stream, _revision + 1, _generation, intent, null, operation,
            firstResponse, null));
        return Result.Success;
    }

    /// <summary>Appends the closure of the pending operation; the caller proved every held action.</summary>
    public Result CompleteClosure(OperationProvenance operation, ImmutableContentRef closureContent,
        ImmutableContentRef actionCompletionProofs, ImmutableContentRef intent, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (_pending is null || _pending.Operation.RootOperationId != operation.RootOperationId ||
            _pending.Intent != intent || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only the exact pending closure can close the finding.", false));
        RaiseEvent(new FindingClosedV2(operation.RootOperationId, _tenantId, _programId, _findingId, _revision + 1,
            closureContent, operation, _generation, actionCompletionProofs, intent, null, operationResult));
        return Result.Success;
    }

    /// <summary>Cancels the pending closure of the operation and fences its delayed completion.</summary>
    public Result CancelClosure(OperationProvenance operation, ImmutableContentRef failureProof)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (_pending is null || _pending.Operation.RootOperationId != operation.RootOperationId ||
            operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only the exact pending closure can be canceled.", false));
        RaiseEvent(new FindingClosureCanceled(Stream, _revision + 1, _generation, _pending.Intent, failureProof,
            operation, null, null));
        return Result.Success;
    }

    /// <summary>Reopens the closed finding as the root native fact of its operation; the generation advances.</summary>
    public Result Reopen(OperationProvenance operation, string reason, ActorReference reopenedBy,
        DateTimeOffset reopenedAt, ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A reopening is a root operation of its own finding with an expected revision."));
        var prepared = PrepareReopen((long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new FindingReopenedV2(operation.LogicalId, _tenantId, _programId, _findingId, _revision + 1,
            reason, reopenedBy, reopenedAt, operation, firstResponse, operationResult));
        return Result.Success;
    }

    void Apply(FindingRaisedV2 ev)
    {
        Acceptance.Observe(ev);
        if (_raised is not null || ev.TenantId != _tenantId || ev.ProgramId != _programId ||
            ev.FindingId != _findingId || ev.Revision != 1 || ev.Operation.Owner != Stream ||
            ev.Operation.LogicalId != _findingId)
            throw new InvalidOperationException("A finding stream is raised once, at revision one, by its own operation.");
        _raised = ev;
        _revision = 1;
        _generation = 1;
        _facts.Add(ev);
    }

    void Apply(FindingRevisedV2 ev)
    {
        Acceptance.Observe(ev);
        EnsureWritable(ev.TenantId, ev.ProgramId, ev.FindingId, ev.Revision, ev.Operation);
        _revised = ev;
        Advance(ev);
    }

    void Apply(CorrectiveActionAdmitted ev)
    {
        Acceptance.Observe(ev);
        if (_raised is null || _pending is not null || _closed is not null || ev.Owner != Stream ||
            ev.Revision != _revision + 1 || ev.Generation != _generation || ev.Operation.Owner != Stream ||
            !_admittedIds.Add(ev.Operation.LogicalId))
            throw new InvalidOperationException(
                "An action is admitted once, for the current open generation, at the next revision.");
        _admitted.Add(ev);
        Advance(ev);
    }

    void Apply(FindingAcceptanceLinkedV2 ev)
    {
        Acceptance.Observe(ev);
        EnsureWritable(ev.TenantId, ev.ProgramId, ev.FindingId, ev.Revision, ev.Operation);
        _acceptances.Add(ev);
        Advance(ev);
    }

    void Apply(FindingClosureAccepted ev)
    {
        Acceptance.Observe(ev);
        if (_raised is null || _pending is not null || _closed is not null || _admitted.Count == 0 ||
            ev.Owner != Stream || ev.Revision != _revision + 1 || ev.Generation != _generation ||
            ev.Operation.Owner != Stream || ev.Operation.RootOperationId != ev.Operation.LogicalId ||
            !_accepted.TryAdd(ev.Operation.RootOperationId, ev))
            throw new InvalidOperationException(
                "A closure is accepted once, with admitted actions, for the current open generation.");
        _pending = ev;
        Advance(ev);
    }

    void Apply(FindingClosedV2 ev)
    {
        Acceptance.Observe(ev);
        if (_pending is null || ev.TenantId != _tenantId || ev.ProgramId != _programId || ev.FindingId != _findingId ||
            ev.Revision != _revision + 1 || ev.FindingGeneration != _generation || ev.Intent != _pending.Intent ||
            ev.Operation.RootOperationId != _pending.Operation.RootOperationId || ev.Operation.Owner != Stream)
            throw new InvalidOperationException("Only the exact pending closure of the current generation closes the finding.");
        _terminal[ev.Operation.RootOperationId] = ev;
        _closed = ev;
        _closures.Add(ev);
        _pending = null;
        Advance(ev);
    }

    void Apply(FindingClosureCanceled ev)
    {
        Acceptance.Observe(ev);
        if (_pending is null || ev.Owner != Stream || ev.Revision != _revision + 1 || ev.Generation != _generation ||
            ev.Intent != _pending.Intent || ev.Operation.RootOperationId != _pending.Operation.RootOperationId ||
            ev.Operation.Owner != Stream)
            throw new InvalidOperationException("Only the exact pending closure of the current generation is canceled.");
        _terminal[ev.Operation.RootOperationId] = ev;
        _pending = null;
        Advance(ev);
    }

    void Apply(FindingReopenedV2 ev)
    {
        Acceptance.Observe(ev);
        if (_raised is null || _closed is null || ev.TenantId != _tenantId || ev.ProgramId != _programId ||
            ev.FindingId != _findingId || ev.Revision != _revision + 1 || ev.Operation.Owner != Stream)
            throw new InvalidOperationException("Only a closed finding is reopened, at the next revision.");
        _closed = null;
        _generation++;
        Advance(ev);
    }

    void EnsureWritable(Uuid tenantId, Uuid programId, Uuid findingId, long revision, OperationProvenance operation)
    {
        if (_raised is null || _pending is not null || _closed is not null || tenantId != _tenantId ||
            programId != _programId || findingId != _findingId || revision != _revision + 1 ||
            operation.Owner != Stream)
            throw new InvalidOperationException("A finding fact follows the raise at the next revision of an open finding.");
    }

    void Advance(DomainEvent fact)
    {
        _revision++;
        _facts.Add(fact);
    }

    Result PrepareOwnWrite(OperationProvenance operation)
    {
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A finding write is a root operation of its own finding with an expected revision."));
        return PrepareWrite((long)expected);
    }

    static Result Fail(CommandFailure failure) => Result.Failure(failure.ToRequestError());
}
