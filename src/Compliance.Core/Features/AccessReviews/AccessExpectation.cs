using Bdgrz.Compliance.Features.AccessControl;
using Bdgrz.Compliance.Features.NativeOperations;
using Bdgrz.Compliance.Features.Proofs;
using Cntryl.Portia;

namespace Bdgrz.Compliance.Features.AccessReviews;

/// <summary>
///     One expectation line of a system instance: the proposals, independent approvals and exceptions of an
///     expectation and of the successors that supersede it. The stream is keyed by the root expectation of the line, so
///     a predecessor and its successors share one conflict key and two successors of one accepted predecessor
///     serialize on the line revision. Unrelated expectation lines never contend. The complete expectation and exception
///     content of each fact is verified immutable content; the rules over that content are decided by the caller from
///     <see cref="AccessExpectationLine" /> and re-checked here against the revision.
/// </summary>
public sealed class AccessExpectation : Aggregate
{
    /// <summary>Stream area of every access expectation line (the realm is the tenant).</summary>
    public const string Area = "access-expectations";

    /// <summary>The status of an expectation that awaits approval.</summary>
    public const string Proposed = "proposed";

    /// <summary>The status of an approved expectation.</summary>
    public const string Approved = "approved";

    readonly Uuid _tenantId;
    readonly List<DomainEvent> _facts = [];
    readonly Dictionary<Uuid, Uuid> _proposers = [];
    readonly HashSet<Uuid> _approved = [];
    Uuid? _systemInstanceId;

    /// <summary>Opens the line of a root expectation, selecting the logical operation to resolve.</summary>
    public AccessExpectation(Uuid tenantId, Uuid lineId, Uuid selectedLogicalId = default)
        : base(lineId, new EventStreamAddress(tenantId.ToString(), Area, lineId.ToString()))
    {
        _tenantId = tenantId;
        Acceptance = new NativeAcceptance(selectedLogicalId);
        On<AccessExpectationProposed>(Apply);
        On<AccessExpectationApproved>(Apply);
        On<AccessExpectationExceptionRecorded>(Apply);
    }

    /// <summary>Acceptance of the logical operation this hydration was selected for.</summary>
    public NativeAcceptance Acceptance { get; }

    /// <summary>The system instance this line belongs to, once the line has a fact.</summary>
    public Uuid? SystemInstanceId => _systemInstanceId;

    /// <summary>The logical revision: the number of facts of this line.</summary>
    public long Revision => _facts.Count;

    /// <summary>Every fact of the line in order.</summary>
    public IReadOnlyList<DomainEvent> Facts => _facts;

    /// <summary>The proposing member of each expectation on the line.</summary>
    public IReadOnlyDictionary<Uuid, Uuid> Proposers => _proposers;

    /// <summary>True when the expectation was proposed on this line.</summary>
    public bool Contains(Uuid expectationId) => _proposers.ContainsKey(expectationId);

    /// <summary>True when the expectation was approved on this line.</summary>
    public bool IsApproved(Uuid expectationId) => _approved.Contains(expectationId);

    /// <summary>Checks the proposal guards without raising anything.</summary>
    public Result PrepareProposal(long expectedRevision, Uuid systemInstanceId, Uuid expectationId)
    {
        if (_systemInstanceId is { } owner && owner != systemInstanceId || _proposers.ContainsKey(expectationId))
            return AccessReviewOperation.Failure(RequestErrorKind.Conflict, "The expectation already exists.");
        return expectedRevision == Revision ? Result.Success : Stale();
    }

    /// <summary>Proposes an expectation as the root native fact of its operation.</summary>
    public Result Propose(OperationProvenance operation, Uuid systemInstanceId, Uuid proposerMemberId,
        ImmutableContentRef expectationContent, ImmutableContentRef firstResponse,
        ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(expectationContent);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return AccessReviewOperation.Failure(RequestErrorKind.Validation,
                "A proposal is a root operation of its own expectation line with an expected revision.");
        var prepared = PrepareProposal((long)expected, systemInstanceId, operation.LogicalId);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new AccessExpectationProposed(_tenantId, systemInstanceId, Revision + 1, proposerMemberId,
            expectationContent, operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Checks the approval guards without raising anything.</summary>
    public Result PrepareApproval(long expectedRevision, Uuid expectationId)
    {
        if (!_proposers.ContainsKey(expectationId))
            return AccessReviewOperation.Failure(RequestErrorKind.NotFound, "The expectation was not found.");
        if (_approved.Contains(expectationId))
            return AccessReviewOperation.Failure(RequestErrorKind.Conflict, "The expectation was already approved.");
        return expectedRevision == Revision ? Result.Success : Stale();
    }

    /// <summary>Approves a proposed expectation as the root native fact of its operation.</summary>
    public Result Approve(OperationProvenance operation, Uuid expectationId, ActorReference approvedBy,
        DateTimeOffset approvedAt, Uuid? waiverId, ImmutableContentRef firstResponse,
        ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(approvedBy);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return AccessReviewOperation.Failure(RequestErrorKind.Validation,
                "An approval is a root operation of its own expectation line with an expected revision.");
        var prepared = PrepareApproval((long)expected, expectationId);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new AccessExpectationApproved(_tenantId, _systemInstanceId!.Value, Revision + 1, expectationId,
            approvedBy, approvedAt, waiverId, operation, firstResponse, operationResult));
        return Result.Success;
    }

    /// <summary>Checks the exception guards without raising anything.</summary>
    public Result PrepareException(long expectedRevision, Uuid expectationId)
    {
        if (!_approved.Contains(expectationId))
            return AccessReviewOperation.Failure(RequestErrorKind.NotFound,
                "The approved expectation was not found.");
        return expectedRevision == Revision ? Result.Success : Stale();
    }

    /// <summary>Records an exception to an approved expectation as the root native fact of its operation.</summary>
    public Result RecordException(OperationProvenance operation, Uuid expectationId,
        ImmutableContentRef exceptionContent, ImmutableContentRef firstResponse, ImmutableContentRef operationResult)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(exceptionContent);
        if (operation.Owner != Stream || operation.OriginalExpectedRevision is not { } expected)
            return AccessReviewOperation.Failure(RequestErrorKind.Validation,
                "An exception is a root operation of its own expectation line with an expected revision.");
        var prepared = PrepareException((long)expected, expectationId);
        if (!prepared.IsSuccess)
            return prepared;
        RaiseEvent(new AccessExpectationExceptionRecorded(_tenantId, _systemInstanceId!.Value, Revision + 1,
            exceptionContent, operation, firstResponse, operationResult));
        return Result.Success;
    }

    void Apply(AccessExpectationProposed ev)
    {
        Acceptance.Observe(ev);
        if (ev.TenantId != _tenantId || _systemInstanceId is { } owner && owner != ev.SystemInstanceId ||
            ev.Revision != Revision + 1 || ev.Operation.Owner != Stream || ev.FirstResponse is null ||
            ev.Operation.LogicalId == Uuid.Empty || !_proposers.TryAdd(ev.Operation.LogicalId, ev.ProposerMemberId))
            throw new InvalidOperationException(
                "An expectation proposal follows the previous revision of its own line and names a new expectation.");
        _systemInstanceId = ev.SystemInstanceId;
        _facts.Add(ev);
    }

    void Apply(AccessExpectationApproved ev)
    {
        Acceptance.Observe(ev);
        if (ev.TenantId != _tenantId || ev.SystemInstanceId != _systemInstanceId || ev.Revision != Revision + 1 ||
            ev.Operation.Owner != Stream || ev.FirstResponse is null || !_proposers.ContainsKey(ev.ExpectationId) ||
            !_approved.Add(ev.ExpectationId))
            throw new InvalidOperationException(
                "An expectation approval follows the previous revision of its own line and names a proposed expectation.");
        _facts.Add(ev);
    }

    void Apply(AccessExpectationExceptionRecorded ev)
    {
        Acceptance.Observe(ev);
        if (ev.TenantId != _tenantId || ev.SystemInstanceId != _systemInstanceId || ev.Revision != Revision + 1 ||
            ev.Operation.Owner != Stream || ev.FirstResponse is null)
            throw new InvalidOperationException(
                "An expectation exception follows the previous revision of its own line.");
        _facts.Add(ev);
    }

    Result Stale() => AccessReviewOperation.Stale("access expectation line", Revision);
}
