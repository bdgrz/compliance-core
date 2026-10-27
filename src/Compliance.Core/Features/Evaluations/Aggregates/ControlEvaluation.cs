using Bdgrz.Compliance.Features.Evaluations;
using Bdgrz.Compliance.Features.AccessControl.Models;
using Bdgrz.Compliance.Features.Evaluations.Events;
using Bdgrz.Compliance.Features.NativeOperations.Models;
using Bdgrz.Compliance.Features.Proofs.Models;
using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Bdgrz.Compliance.Features.Versioning;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.Evaluations.Aggregates;

/// <summary>
///     One evaluation of one control: its start against an exact plan and control version, every step result, minor
///     deviation disposition, submitted round and independent review. Its identity is the evaluation id, so different
///     evaluations never share a stream and a retest is a new evaluation that names, but never rewrites, its
///     predecessor. Its logical revision counts the facts of this evaluation only. The product rules over step,
///     deviation, submission and review content are decided by the caller from <see cref="ControlEvaluationRules" /> and
///     re-checked here against the revision and the lifecycle.
/// </summary>
public sealed class ControlEvaluation : Aggregate
{
    /// <summary>Stream area of every control evaluation (the realm is the tenant).</summary>
    public const string Area = "control-evaluations";

    readonly Uuid _tenantId;
    DomainEvent? _latest;
    ControlEvaluationReviewed? _acceptingReview;
    ControlEvaluationStarted? _started;
    Uuid _programId;
    Uuid _controlId;
    string _lifecycle = string.Empty;
    long _revision;

    /// <summary>
    ///     Opens the evaluation with a known id, selecting the logical operation to resolve. The program and control
    ///     are learned from the start fact, so a request that names another program or control is recognizable.
    /// </summary>
    public ControlEvaluation(Uuid tenantId, Uuid evaluationId, Uuid selectedLogicalId = default)
        : base(evaluationId, new EventStreamAddress(tenantId.ToString(), Area, evaluationId.ToString()))
    {
        _tenantId = tenantId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<ControlEvaluationStarted>(Apply);
        On<ControlEvaluationStepRecorded>(Apply);
        On<ControlEvaluationDeviationDisposed>(Apply);
        On<ControlEvaluationSubmitted>(Apply);
        On<ControlEvaluationReviewed>(Apply);
    }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The program this evaluation belongs to; empty before the start.</summary>
    public Uuid ProgramId => _programId;

    internal Uuid TenantId => _tenantId;

    /// <summary>The control this evaluation evaluates; empty before the start.</summary>
    public Uuid ControlId => _controlId;

    /// <summary>Whether the evaluation was started for this program and control.</summary>
    public bool Belongs(Uuid programId, Uuid controlId) =>
        _started is not null && _programId == programId && _controlId == controlId;

    /// <summary>The logical revision: the number of facts of this evaluation.</summary>
    public long Revision => _revision;

    /// <summary>The start fact, once recorded.</summary>
    public ControlEvaluationStarted? Started => _started;

    /// <summary>The lifecycle: empty before the start, then in_progress, submitted or accepted.</summary>
    public string Lifecycle => _lifecycle;

    /// <summary>The latest native fact of this evaluation.</summary>
    public DomainEvent? Latest => _latest;

    internal EvaluationReplayCut ReplayCut { get; } = new();

    /// <summary>The fact that accepted the evaluation, once it is accepted.</summary>
    public ControlEvaluationReviewed? AcceptingReview => _acceptingReview;

    /// <summary>Checks the start guard without raising anything.</summary>
    public Result PrepareStart() => _started is null
        ? Result.Success
        : Result.Failure(new RequestError(RequestErrorKind.Conflict, "The evaluation was already started."));

    /// <summary>Starts the evaluation as the root native fact of its own stream.</summary>
    public Result Start(OperationProvenance operation, Uuid programId, Uuid controlId, Uuid controlVersionId,
        Uuid planVersionId, long planVersion,
        ImmutableContentRef stepsManifest, Uuid evaluatorMemberId, ActorReference startedBy, DateTimeOffset startedAt,
        Uuid? retestOfEvaluationId, NativeFactReference? retestPredecessor, ImmutableContentRef retestDeviationIds,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(stepsManifest);
        ArgumentNullException.ThrowIfNull(retestDeviationIds);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not null || operation.LogicalId != Id)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A start is the root operation that creates its own evaluation."));
        var prepared = PrepareStart();
        if (!prepared.IsSuccess)
            return prepared;
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        RaiseEvent(new ControlEvaluationStarted(_tenantId, programId, controlId, Id, 1, controlVersionId,
            planVersionId, planVersion, stepsManifest, evaluatorMemberId, startedBy, startedAt, retestOfEvaluationId,
            retestPredecessor, retestDeviationIds, operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Checks the guard of evaluator work (a step result, a disposition or a submission).</summary>
    public Result PrepareWork(long expectedRevision)
    {
        if (_started is null)
            return NotFound();
        if (expectedRevision != _revision)
            return Stale();
        return _lifecycle == ControlEvaluationStates.InProgress
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The evaluation is not open for evaluator work."));
    }

    /// <summary>Records a step result as the root native fact of its operation.</summary>
    public Result RecordStep(OperationProvenance operation, Uuid stepId, ImmutableContentRef result,
        ImmutableContentRef? deviation, ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(result);
        var prepared = PrepareRoot(operation);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new ControlEvaluationStepRecorded(_tenantId, _programId, _controlId, Id, _revision + 1, stepId,
            result, deviation, operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Records a minor deviation's disposition as the root native fact of its operation.</summary>
    public Result DisposeDeviation(OperationProvenance operation, Uuid deviationId, string disposition, string rationale,
        Uuid? waiverId, ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var prepared = PrepareRoot(operation);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new ControlEvaluationDeviationDisposed(_tenantId, _programId, _controlId, Id, _revision + 1,
            deviationId, disposition, rationale.Trim(), waiverId, operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Submits the round as the root native fact of its operation.</summary>
    public Result Submit(OperationProvenance operation, ImmutableContentRef submission,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(submission);
        var prepared = PrepareRoot(operation);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new ControlEvaluationSubmitted(_tenantId, _programId, _controlId, Id, _revision + 1, submission,
            operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Checks the guard of an independent review.</summary>
    public Result PrepareReview(long expectedRevision)
    {
        if (_started is null)
            return NotFound();
        if (expectedRevision != _revision)
            return Stale();
        return _lifecycle == ControlEvaluationStates.Submitted
            ? Result.Success
            : Result.Failure(new RequestError(RequestErrorKind.Conflict,
                "The evaluation has no submission awaiting review."));
    }

    /// <summary>Reviews the submitted round as the root native fact of its operation.</summary>
    public Result Review(OperationProvenance operation, string decision, ImmutableContentRef review,
        ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(review);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "A review is a root operation of its own evaluation with an expected revision."));
        var prepared = PrepareReview((long)expected);
        if (!prepared.IsSuccess)
            return prepared;
        var cut = ReplayCut.Require(this, Acceptance, operation);
        if (!cut.IsSuccess)
            return cut;
        RaiseEvent(new ControlEvaluationReviewed(_tenantId, _programId, _controlId, Id, _revision + 1, decision, review,
            operation, firstResponse, operationResult));
        return Result.Success;
    }

    Result PrepareRoot(OperationProvenance operation)
    {
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return Result.Failure(new RequestError(RequestErrorKind.Validation,
                "Evaluator work is a root operation of its own evaluation with an expected revision."));
        var prepared = PrepareWork((long)expected);
        return prepared.IsSuccess ? ReplayCut.Require(this, Acceptance, operation) : prepared;
    }

    void Apply(ControlEvaluationStarted ev)
    {
        Acceptance.Observe(ev);
        if (_started is not null || ev.TenantId != _tenantId || ev.EvaluationId != Id || ev.Revision != 1 ||
            ev.Operation.Owner != Stream || ev.Operation.LogicalId != Id || ev.FirstResponse is null)
            throw new InvalidOperationException(
                "An evaluation starts once, on the stream its evaluation id names, at its first revision.");
        _started = ev;
        (_programId, _controlId) = (ev.ProgramId, ev.ControlId);
        _lifecycle = ControlEvaluationStates.InProgress;
        Advance(ev);
    }

    void Apply(ControlEvaluationStepRecorded ev)
    {
        Acceptance.Observe(ev);
        Require(ev.TenantId, ev.ProgramId, ev.ControlId, ev.EvaluationId, ev.Revision, ControlEvaluationStates.InProgress,
            ev.Operation, ev.FirstResponse);
        Advance(ev);
    }

    void Apply(ControlEvaluationDeviationDisposed ev)
    {
        Acceptance.Observe(ev);
        Require(ev.TenantId, ev.ProgramId, ev.ControlId, ev.EvaluationId, ev.Revision, ControlEvaluationStates.InProgress,
            ev.Operation, ev.FirstResponse);
        Advance(ev);
    }

    void Apply(ControlEvaluationSubmitted ev)
    {
        Acceptance.Observe(ev);
        Require(ev.TenantId, ev.ProgramId, ev.ControlId, ev.EvaluationId, ev.Revision, ControlEvaluationStates.InProgress,
            ev.Operation, ev.FirstResponse);
        _lifecycle = ControlEvaluationStates.Submitted;
        Advance(ev);
    }

    void Apply(ControlEvaluationReviewed ev)
    {
        Acceptance.Observe(ev);
        Require(ev.TenantId, ev.ProgramId, ev.ControlId, ev.EvaluationId, ev.Revision, ControlEvaluationStates.Submitted,
            ev.Operation, ev.FirstResponse);
        _acceptingReview = ev.Decision == ControlEvaluationStates.Accepted ? ev : null;
        _lifecycle = ev.Decision == ControlEvaluationStates.Accepted
            ? ControlEvaluationStates.Accepted
            : ControlEvaluationStates.InProgress;
        Advance(ev);
    }

    void Require(Uuid tenantId, Uuid programId, Uuid controlId, Uuid evaluationId, long revision, string lifecycle,
        OperationProvenance operation, ImmutableContentRef? firstResponse)
    {
        if (_started is null || tenantId != _tenantId || programId != _programId || controlId != _controlId ||
            evaluationId != Id || revision != _revision + 1 || _lifecycle != lifecycle || operation.Owner != Stream ||
            firstResponse is null)
            throw new InvalidOperationException(
                "An evaluation fact follows the previous revision of its own evaluation in the lifecycle it needs.");
    }

    void Advance(DomainEvent ev)
    {
        _revision++;
        _latest = ev;
    }

    protected override void Apply(DomainEvent ev)
    {
        base.Apply(ev);
        ReplayCut.Observe(ev);
    }

    static Result NotFound() => Result.Failure(new RequestError(RequestErrorKind.NotFound,
        "The control evaluation was not found."));

    Result Stale() => Result.Failure(VersionedRecordRules.StaleRevision("control evaluation", _revision)
        .ToRequestError());
}
