using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AuditorRequests;

/// <summary>
///     One auditor request: its terms, the admitted responses, comments and sample selections of its generations, and
///     the resolution decision. The logical revision counts every fact of this request stream and the generation starts
///     at one and advances on every reopen. A response, comment or selection is admitted here and materialized as its own
///     owner; a resolution seals admission for its generation and only resolves once the resolution process appends
///     <see cref="AuditorRequestResolved" />. Reducers never read content or another owner.
/// </summary>
public sealed class AuditorRequest : Aggregate
{
    /// <summary>Stream area of every auditor request (the realm is the tenant).</summary>
    public const string Area = "auditor-requests";

    readonly Uuid _tenantId;
    readonly Uuid _programId;
    readonly Uuid _selectedResolutionId;
    AuditorRequestResolutionAccepted? _selectedResolution;
    DomainEvent? _selectedTerminal;
    DomainEvent? _latest;
    long _responseCount;
    AuditorRequestRecorded? _recorded;
    ImmutableContentRef? _terms;
    AuditorRequestResolutionAccepted? _pending;
    AuditorRequestResolved? _resolved;
    long _revision;
    long _generation;

    /// <summary>Opens one request, selecting the logical operation to resolve.</summary>
    public AuditorRequest(Uuid tenantId, Uuid programId, Uuid requestId, Uuid selectedLogicalId = default,
        Uuid selectedResolutionId = default)
        : base(requestId, new EventStreamAddress(tenantId.ToString(), Area, requestId.ToString()))
    {
        if (tenantId == Uuid.Empty || programId == Uuid.Empty || requestId == Uuid.Empty)
            throw new ArgumentException("An auditor request stream needs tenant, program, and request IDs.");
        (_tenantId, _programId) = (tenantId, programId);
        Acceptance = new NativeAcceptance(selectedLogicalId);
        _selectedResolutionId = selectedResolutionId;
        On<AuditorRequestRecorded>(Apply);
        On<AuditorRequestRevised>(Apply);
        On<AuditorRequestAssigned>(Apply);
        On<AuditorResponseAdmitted>(Apply);
        On<AuditorCommentAdmitted>(Apply);
        On<AuditorSampleSelectionAdmitted>(Apply);
        On<AuditorRequestResolutionAccepted>(Apply);
        On<AuditorRequestResolved>(Apply);
        On<AuditorRequestResolutionCanceled>(Apply);
        On<AuditorRequestReopened>(Apply);
    }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The program the request belongs to.</summary>
    public Uuid ProgramId => _programId;

    /// <summary>The request identity.</summary>
    public Uuid RequestId => Id;

    /// <summary>True once the request was recorded.</summary>
    public bool IsRecorded => _recorded is not null;

    /// <summary>The recording fact, once recorded.</summary>
    public AuditorRequestRecorded? Recorded => _recorded;

    /// <summary>The logical revision: the number of facts of this request.</summary>
    public long Revision => _revision;

    /// <summary>The current generation: one at the recording, plus one for every reopen.</summary>
    public long Generation => _generation;

    /// <summary>The verified content of the current terms (recording, last revision or last assignment).</summary>
    public ImmutableContentRef? Terms => _terms;

    /// <summary>The latest content fact, independent of the selected operation.</summary>
    public DomainEvent? Latest => _latest;

    /// <summary>The tenant realm of this owner.</summary>
    public Uuid TenantId => _tenantId;

    /// <summary>The number of admitted responses; complete membership lives in verified read composition.</summary>
    public long ResponseCount => _responseCount;

    /// <summary>The resolution accepted and not yet resolved or canceled, which seals admission.</summary>
    public AuditorRequestResolutionAccepted? PendingResolution => _pending;

    /// <summary>The resolution fact of the current generation; null while open or after a reopen.</summary>
    public AuditorRequestResolved? Resolved => _resolved;

    /// <summary>True while the request admits work.</summary>
    public bool IsOpen => _recorded is not null && _resolved is null && _pending is null;

    /// <summary>The explicitly selected historical resolution, or the current pending resolution.</summary>
    public AuditorRequestResolutionAccepted? AcceptedResolution(Uuid rootOperationId) =>
        _selectedResolution?.Operation.RootOperationId == rootOperationId ? _selectedResolution :
        _pending?.Operation.RootOperationId == rootOperationId ? _pending : null;

    /// <summary>The terminal fact explicitly selected before hydration, or the current resolved fact.</summary>
    public DomainEvent? TerminalResolution(Uuid rootOperationId) =>
        _selectedTerminal is INativeOperationFact selected && selected.Operation.RootOperationId == rootOperationId
            ? _selectedTerminal : _resolved?.Operation.RootOperationId == rootOperationId ? _resolved : null;

    /// <summary>Checks that the request does not exist yet.</summary>
    public Result PrepareRecord() => IsRecorded
        ? Fail(CommandFailure.StateConflict("The auditor request already exists with different terms or attribution."))
        : Result.Success;

    /// <summary>
    ///     Checks that the open request can take a write at the expected revision. <paramref name="resolvedMessage" /> is
    ///     the conflict reported when the request is resolved and must be reopened first.
    /// </summary>
    public Result PrepareWrite(long expectedRevision, string resolvedMessage)
    {
        if (!IsRecorded)
            return Fail(CommandFailure.MissingRecord("The auditor request was not found."));
        if (expectedRevision != _revision)
            return Fail(CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("auditor request", _revision)));
        if (_pending is not null)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "A resolution of this auditor request is in progress; retry when it settles.", true));
        return _resolved is not null ? Fail(CommandFailure.StateConflict(resolvedMessage)) : Result.Success;
    }

    /// <summary>Checks that a resolution of exactly the admitted responses can be accepted at the expected revision.</summary>
    public Result PrepareResolution(long expectedRevision, IReadOnlyCollection<Uuid> responseIds,
        AuditorRequestHistorySnapshot history)
    {
        ArgumentNullException.ThrowIfNull(responseIds);
        var prepared = PrepareWrite(expectedRevision, "The auditor request is already resolved.");
        if (!prepared.IsSuccess)
            return prepared;
        return history.ValidateResponses(this, responseIds);
    }

    /// <summary>Checks that a resolved request can be reopened at the expected revision.</summary>
    public Result PrepareReopen(long expectedRevision)
    {
        if (!IsRecorded)
            return Fail(CommandFailure.MissingRecord("The auditor request was not found."));
        if (expectedRevision != _revision)
            return Fail(CommandFailure.ForVersion(VersionedRecordRules.StaleRevision("auditor request", _revision)));
        return _resolved is null
            ? Fail(CommandFailure.StateConflict("Only a resolved auditor request can be reopened."))
            : Result.Success;
    }

    /// <summary>
    ///     Checks that a sample selection can be admitted at the expected revision: a superseded selection must be a
    ///     prior admitted selection of the same population with a strictly later population version.
    /// </summary>
    public Result PrepareSelection(long expectedRevision, Uuid selectionId, Uuid? supersedesSelectionId,
        Uuid populationId, int populationVersion, AuditorRequestHistorySnapshot history)
    {
        var prepared = PrepareWrite(expectedRevision, "A sample selection can only be recorded for an open request.");
        if (!prepared.IsSuccess)
            return prepared;
        return history.ValidateSelection(this, selectionId, supersedesSelectionId, populationId, populationVersion);
    }

    /// <summary>Records the request as the root native fact of its operation.</summary>
    public Result Record(OperationProvenance operation, ImmutableContentRef terms, Uuid actorMemberId,
        DateTimeOffset at, ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(terms);
        if (operation.Owner != Stream || operation.LogicalId != Id)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A recording is the logical operation of its own request."));
        var prepared = PrepareRecord();
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new AuditorRequestRecorded(_tenantId, Id, 1, actorMemberId, terms, at, operation, firstResponse,
            operationResult));
        return Result.Success;
    }

    /// <summary>Records a terms revision as the root native fact of its operation.</summary>
    public Result Revise(OperationProvenance operation, ImmutableContentRef terms, Uuid actorMemberId, DateTimeOffset at,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        var prepared = PrepareOwnWrite(operation, "A resolved auditor request must be reopened before revision.");
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new AuditorRequestRevised(_tenantId, Id, _revision + 1, actorMemberId, terms, at, operation,
            firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Records an assignment as the root native fact of its operation.</summary>
    public Result Assign(OperationProvenance operation, ImmutableContentRef terms, Uuid actorMemberId, DateTimeOffset at,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        var prepared = PrepareOwnWrite(operation, "A resolved auditor request must be reopened before reassignment.");
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new AuditorRequestAssigned(_tenantId, Id, _revision + 1, actorMemberId, terms, at, operation,
            firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Admits one response draft for the current generation as the root native fact of its operation.</summary>
    public Result AdmitResponse(OperationProvenance operation, ImmutableContentRef intent, ImmutableContentRef firstResponse)
    {
        ArgumentNullException.ThrowIfNull(intent);
        var prepared = PrepareOwnWrite(operation, "A response can only be drafted for an open request.");
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new AuditorResponseAdmitted(Stream, _revision + 1, _generation, intent, null, operation,
            firstResponse, null));
        return Result.Success;
    }

    /// <summary>Admits one comment for the current generation as the root native fact of its operation.</summary>
    public Result AdmitComment(OperationProvenance operation, ImmutableContentRef intent, ImmutableContentRef firstResponse)
    {
        ArgumentNullException.ThrowIfNull(intent);
        var prepared = PrepareOwnWrite(operation, "Discussion can only be added to an open request.");
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new AuditorCommentAdmitted(Stream, _revision + 1, _generation, intent, null, operation,
            firstResponse, null));
        return Result.Success;
    }

    /// <summary>Admits one sample selection for the current generation as the root native fact of its operation.</summary>
    public Result AdmitSelection(OperationProvenance operation, ImmutableContentRef intent, Uuid populationId,
        int populationVersion, Uuid? supersedesSelectionId, ImmutableContentRef firstResponse,
        AuditorRequestHistorySnapshot history)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A request write is a root operation of its own request with an expected revision."));
        var prepared = PrepareSelection((long)expected, operation.LogicalId, supersedesSelectionId, populationId,
            populationVersion, history);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new AuditorSampleSelectionAdmitted(Stream, _revision + 1, _generation, intent, null, operation,
            firstResponse, null, populationId, populationVersion, supersedesSelectionId));
        return Result.Success;
    }

    /// <summary>Accepts a resolution as the root native fact of its operation; it seals admission for the generation.</summary>
    public Result AcceptResolution(OperationProvenance operation, ImmutableContentRef intent,
        IReadOnlyCollection<Uuid> responseIds, ImmutableContentRef firstResponse, AuditorRequestHistorySnapshot history)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(intent);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A resolution is a root operation of its own request with an expected revision."));
        var prepared = PrepareResolution((long)expected, responseIds, history);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new AuditorRequestResolutionAccepted(Stream, _revision + 1, _generation, intent, null, operation,
            firstResponse, null));
        return Result.Success;
    }

    /// <summary>Appends the resolution of the pending operation; the caller proved every held response.</summary>
    public Result CompleteResolution(OperationProvenance operation, Uuid? actorMemberId, ImmutableContentRef intent,
        ImmutableContentRef responseDispositionProofs, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (_pending is null || _pending.Operation.RootOperationId != operation.RootOperationId ||
            _pending.Intent != intent || operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only the exact pending resolution can resolve the request.", false));
        RaiseEvent(new AuditorRequestResolved(_tenantId, Id, _revision + 1, actorMemberId, intent,
            operation.AcceptedAt, operation, _generation, responseDispositionProofs, null, operationResult));
        return Result.Success;
    }

    /// <summary>Cancels the pending resolution of the operation and fences its delayed completion.</summary>
    public Result CancelResolution(OperationProvenance operation, ImmutableContentRef failureProof)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (_pending is null || _pending.Operation.RootOperationId != operation.RootOperationId ||
            operation.Owner != Stream)
            return Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "Only the exact pending resolution can be canceled.", false));
        RaiseEvent(new AuditorRequestResolutionCanceled(Stream, _revision + 1, _generation, _pending.Intent,
            failureProof, operation, null, null));
        return Result.Success;
    }

    /// <summary>Reopens the resolved request as the root native fact of its operation; the generation advances.</summary>
    public Result Reopen(OperationProvenance operation, ImmutableContentRef reopen, Uuid actorMemberId, DateTimeOffset at,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A reopening is a root operation of its own request with an expected revision."));
        var prepared = PrepareReopen((long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new AuditorRequestReopened(_tenantId, Id, _revision + 1, actorMemberId, reopen, at, operation,
            firstResponse, operationResult));
        return Result.Success;
    }

    void Apply(AuditorRequestRecorded ev)
    {
        Acceptance.Observe(ev);
        if (_recorded is not null || ev.TenantId != _tenantId || ev.SubjectId != Id || ev.Revision != 1 ||
            ev.Operation.Owner != Stream || ev.Operation.LogicalId != Id)
            throw new InvalidOperationException("A request stream is recorded once, at revision one, by its own operation.");
        _recorded = ev;
        _terms = ev.Change;
        _revision = 1;
        _generation = 1;
        _latest = ev;
    }

    void Apply(AuditorRequestRevised ev)
    {
        Acceptance.Observe(ev);
        EnsureWritable(ev.TenantId, ev.SubjectId, ev.Revision, ev.Operation);
        _terms = ev.Change;
        Advance(ev);
    }

    void Apply(AuditorRequestAssigned ev)
    {
        Acceptance.Observe(ev);
        EnsureWritable(ev.TenantId, ev.SubjectId, ev.Revision, ev.Operation);
        _terms = ev.Change;
        Advance(ev);
    }

    void Apply(AuditorResponseAdmitted ev)
    {
        Acceptance.Observe(ev);
        EnsureAdmission(ev.Owner, ev.Revision, ev.Generation, ev.Operation);
        _responseCount++;
        Advance(ev);
    }

    void Apply(AuditorCommentAdmitted ev)
    {
        Acceptance.Observe(ev);
        EnsureAdmission(ev.Owner, ev.Revision, ev.Generation, ev.Operation);
        Advance(ev);
    }

    void Apply(AuditorSampleSelectionAdmitted ev)
    {
        Acceptance.Observe(ev);
        EnsureAdmission(ev.Owner, ev.Revision, ev.Generation, ev.Operation);
        if (ev.PopulationId == Uuid.Empty || ev.PopulationVersion <= 0)
            throw new InvalidOperationException("A selection names one population version.");
        Advance(ev);
    }

    void Apply(AuditorRequestResolutionAccepted ev)
    {
        Acceptance.Observe(ev);
        if (_recorded is null || _pending is not null || _resolved is not null || _responseCount == 0 ||
            ev.Owner != Stream || ev.Revision != _revision + 1 || ev.Generation != _generation ||
            ev.Operation.Owner != Stream || ev.Operation.RootOperationId != ev.Operation.LogicalId)
            throw new InvalidOperationException(
                "A resolution is accepted once, with admitted responses, for the current open generation.");
        if (ev.Operation.RootOperationId == _selectedResolutionId)
            _selectedResolution = ev;
        _pending = ev;
        Advance(ev);
    }

    void Apply(AuditorRequestResolved ev)
    {
        Acceptance.Observe(ev);
        if (_pending is null || ev.TenantId != _tenantId || ev.SubjectId != Id || ev.Revision != _revision + 1 ||
            ev.RequestGeneration != _generation || ev.Change != _pending.Intent ||
            ev.Operation.RootOperationId != _pending.Operation.RootOperationId || ev.Operation.Owner != Stream)
            throw new InvalidOperationException("Only the exact pending resolution of the current generation resolves the request.");
        if (ev.Operation.RootOperationId == _selectedResolutionId)
            _selectedTerminal = ev;
        _resolved = ev;
        _pending = null;
        Advance(ev);
    }

    void Apply(AuditorRequestResolutionCanceled ev)
    {
        Acceptance.Observe(ev);
        if (_pending is null || ev.Owner != Stream || ev.Revision != _revision + 1 || ev.Generation != _generation ||
            ev.Intent != _pending.Intent || ev.Operation.RootOperationId != _pending.Operation.RootOperationId ||
            ev.Operation.Owner != Stream)
            throw new InvalidOperationException("Only the exact pending resolution of the current generation is canceled.");
        if (ev.Operation.RootOperationId == _selectedResolutionId)
            _selectedTerminal = ev;
        _pending = null;
        Advance(ev);
    }

    void Apply(AuditorRequestReopened ev)
    {
        Acceptance.Observe(ev);
        if (_recorded is null || _resolved is null || ev.TenantId != _tenantId || ev.SubjectId != Id ||
            ev.Revision != _revision + 1 || ev.Operation.Owner != Stream)
            throw new InvalidOperationException("Only a resolved request is reopened, at the next revision.");
        _resolved = null;
        _generation++;
        Advance(ev);
    }

    void EnsureWritable(Uuid tenantId, Uuid subjectId, long revision, OperationProvenance operation)
    {
        if (_recorded is null || _pending is not null || _resolved is not null || tenantId != _tenantId ||
            subjectId != Id || revision != _revision + 1 || operation.Owner != Stream)
            throw new InvalidOperationException("A request fact follows the recording at the next revision of an open request.");
    }

    void EnsureAdmission(EventStreamAddress owner, long revision, long generation, OperationProvenance operation)
    {
        if (_recorded is null || _pending is not null || _resolved is not null || owner != Stream ||
            revision != _revision + 1 || generation != _generation || operation.Owner != Stream)
            throw new InvalidOperationException(
                "A child is admitted for the current open generation at the next revision.");
    }

    void Advance(DomainEvent fact)
    {
        _revision++;
        _latest = fact;
    }

    Result PrepareOwnWrite(OperationProvenance operation, string resolvedMessage)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A request write is a root operation of its own request with an expected revision."));
        return PrepareWrite((long)expected, resolvedMessage);
    }

    static Result Fail(CommandFailure failure) => Result.Failure(failure.ToRequestError());
}
